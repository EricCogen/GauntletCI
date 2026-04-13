// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Discovery;

public sealed class GitHubSearchDiscoveryProvider : IDiscoveryProvider
{
    private readonly HttpClient _http;

    public GitHubSearchDiscoveryProvider(string githubToken)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
            throw new InvalidOperationException("GITHUB_TOKEN is required for gh-search provider");

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public string GetProviderName() => "gh-search";

    public bool SupportsIncrementalSync => true;

    public async Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken cancellationToken = default)
    {
        var seen    = new HashSet<(string Owner, string Repo, int Number)>();
        var results = new List<PullRequestCandidate>();

        var languages = query.Languages.Count > 0
            ? (IEnumerable<string>)query.Languages
            : new[] { "" };

        foreach (var lang in languages)
        {
            if (results.Count >= query.MaxCandidates)
                break;

            var q   = BuildQuery(query, lang);
            var url = $"https://api.github.com/search/issues?q={Uri.EscapeDataString(q)}&sort=updated&order=desc&per_page=100&page=1";

            using var response = await _http.GetAsync(url, cancellationToken);

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) &&
                int.TryParse(remaining.FirstOrDefault(), out var remainingCount) &&
                remainingCount <= 0)
            {
                Console.Error.WriteLine("[gh-search] GitHub rate limit reached; returning partial results.");
                break;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("items", out var items))
                continue;

            foreach (var item in items.EnumerateArray())
            {
                if (results.Count >= query.MaxCandidates)
                    break;

                var candidate = MapToCandidate(item, lang);
                if (candidate is null)
                    continue;

                var key = (candidate.RepoOwner, candidate.RepoName, candidate.PullRequestNumber);
                if (!seen.Add(key))
                    continue;

                var fullRepo = $"{candidate.RepoOwner}/{candidate.RepoName}";

                if (query.RepoBlockList.Count > 0 &&
                    query.RepoBlockList.Any(r => string.Equals(r, fullRepo, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (query.RepoAllowList.Count > 0 &&
                    !query.RepoAllowList.Any(r => string.Equals(r, fullRepo, StringComparison.OrdinalIgnoreCase)))
                    continue;

                results.Add(candidate);
            }
        }

        return results;
    }

    private static string BuildQuery(DiscoveryQuery query, string lang)
    {
        var parts = new List<string> { "is:pr", "is:merged" };

        if (query.MinReviewComments > 0)
            parts.Add($"comments:>{query.MinReviewComments}");

        if (query.MinStars > 0)
            parts.Add($"stars:>{query.MinStars}");

        if (!string.IsNullOrEmpty(lang))
            parts.Add($"language:{lang}");

        if (query.StartDateUtc.HasValue)
            parts.Add($"merged:>={query.StartDateUtc.Value:yyyy-MM-dd}");

        if (query.EndDateUtc.HasValue)
            parts.Add($"merged:<={query.EndDateUtc.Value:yyyy-MM-dd}");

        return string.Join(" ", parts);
    }

    private static PullRequestCandidate? MapToCandidate(JsonElement item, string lang)
    {
        if (!item.TryGetProperty("repository_url", out var repoUrlEl))
            return null;

        var repoUrl  = repoUrlEl.GetString() ?? "";
        var repoPath = repoUrl.Replace("https://api.github.com/repos/", "", StringComparison.Ordinal);
        var repoParts = repoPath.Split('/', 2);
        if (repoParts.Length < 2)
            return null;

        var owner = repoParts[0];
        var repo  = repoParts[1];

        if (!item.TryGetProperty("number", out var numEl))
            return null;

        var prNumber  = numEl.GetInt32();
        var htmlUrl   = item.TryGetProperty("html_url",   out var urlEl)      ? urlEl.GetString()      ?? "" : "";
        var createdAt = item.TryGetProperty("created_at", out var createdEl)  ? createdEl.GetDateTime()     : DateTime.UtcNow;
        var updatedAt = item.TryGetProperty("updated_at", out var updatedEl)  ? updatedEl.GetDateTime()     : DateTime.UtcNow;
        var comments  = item.TryGetProperty("comments",   out var commentsEl) ? commentsEl.GetInt32()       : 0;
        var isDraft   = item.TryGetProperty("draft",      out var draftEl)    && draftEl.GetBoolean();

        return new PullRequestCandidate
        {
            Source             = "gh-search",
            RepoOwner          = owner,
            RepoName           = repo,
            PullRequestNumber  = prNumber,
            Url                = htmlUrl,
            Language           = lang,
            CreatedAtUtc       = createdAt,
            UpdatedAtUtc       = updatedAt,
            ReviewCommentCount = comments,
            IsDraft            = isDraft,
            MergeState         = MergeState.Merged,
            CandidateReason    = "gh-search",
        };
    }
}
