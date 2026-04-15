// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Discovery;

public sealed class GitHubSearchDiscoveryProvider : IDiscoveryProvider
{
    private const int ThrottleThreshold = 5;

    private readonly HttpClient _http;
    private readonly Action<string?, int?, string>? _errorCallback;

    public int? LastSearchRemaining { get; private set; }
    public int? LastSearchLimit { get; private set; }
    public DateTimeOffset? LastSearchResetUtc { get; private set; }
    public int ThrottleCount { get; private set; }

    public GitHubSearchDiscoveryProvider(string githubToken, Action<string?, int?, string>? errorCallback = null)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
            throw new InvalidOperationException("GITHUB_TOKEN is required for gh-search provider");

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _errorCallback = errorCallback;
    }

    public string GetProviderName() => "gh-search";

    public bool SupportsIncrementalSync => true;

    public async Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken cancellationToken = default)
    {
        var seen    = new HashSet<(string Owner, string Repo, int Number)>();
        var results = new List<PullRequestCandidate>();

        if (query.RepoAllowList.Count == 0)
            throw new InvalidOperationException(
                "gh-search requires a repo allowlist. " +
                "Pass --repo-allowlist owner/repo (repeatable) or use -RepoAllowlist in run-corpus.ps1. " +
                "Global keyword search is disabled to prevent low-quality corpus ingestion.");

        // Allowlist mode: one targeted repo: query per known repo
        foreach (var repoSpec in query.RepoAllowList)
        {
            if (results.Count >= query.MaxCandidates)
                break;

            var q   = BuildRepoQuery(query, repoSpec);
            var url = $"https://api.github.com/search/issues?q={Uri.EscapeDataString(q)}&sort=updated&order=desc&per_page=100&page=1";

            var repoLimit = query.PerRepoLimit > 0
                ? Math.Min(query.PerRepoLimit, query.MaxCandidates - results.Count)
                : query.MaxCandidates - results.Count;

            try
            {
                await FetchPageAsync(url, query, seen, results, repoLimit, cancellationToken, _errorCallback);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity
                                               || ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                               || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var code = (int?)ex.StatusCode;
                var kind = code switch
                {
                    422 => "QueryError",
                    404 => "NotFound",
                    403 => "IpBlockOrAbuse",
                    _   => "HttpError"
                };
                Console.Error.WriteLine($"[gh-search] {kind}: Skipping {repoSpec} ({code})");
                _errorCallback?.Invoke(repoSpec, code, $"[gh-search] {kind}: Skipping {repoSpec} ({code})");
            }
        }

        return results;
    }

    private async Task FetchPageAsync(
        string url,
        DiscoveryQuery query,
        HashSet<(string, string, int)> seen,
        List<PullRequestCandidate> results,
        int maxFromThisCall,
        CancellationToken cancellationToken,
        Action<string?, int?, string>? errorCallback = null)
    {
        using var response = await _http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var code = (int)response.StatusCode;
            var classification = ClassifyHttpError(response, body);
            Console.Error.WriteLine($"[gh-search] {classification} for {url}");
            errorCallback?.Invoke(null, code, $"[gh-search] {classification} | url={url}");
            response.EnsureSuccessStatusCode();
        }

        // Parse rate limit headers and update tracking properties
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingVals) &&
            int.TryParse(remainingVals.FirstOrDefault(), out var remaining))
        {
            LastSearchRemaining = remaining;

            if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitVals) &&
                int.TryParse(limitVals.FirstOrDefault(), out var limit))
                LastSearchLimit = limit;

            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetVals) &&
                long.TryParse(resetVals.FirstOrDefault(), out var resetEpoch))
                LastSearchResetUtc = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);

            if (remaining <= 0)
            {
                var msg = "[gh-search] GitHub rate limit reached; returning partial results.";
                Console.Error.WriteLine(msg);
                errorCallback?.Invoke(null, 429, msg);
                return;
            }

            if (remaining <= ThrottleThreshold)
            {
                var sleepMs = Math.Max(0, (int)(LastSearchResetUtc!.Value - DateTimeOffset.UtcNow).TotalMilliseconds) + 1000;
                Console.Error.WriteLine($"[gh-search] Throttling: {remaining} search requests left. Sleeping {sleepMs / 1000}s until reset...");
                errorCallback?.Invoke(null, null, $"[gh-search] Throttled: {remaining} remaining, sleeping {sleepMs / 1000}s");
                ThrottleCount++;
                await Task.Delay(sleepMs, cancellationToken);
            }
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("items", out var items))
            return;

        int addedFromThisCall = 0;

        foreach (var item in items.EnumerateArray())
        {
            if (addedFromThisCall >= maxFromThisCall)
                break;

            var candidate = MapToCandidate(item, "");
            if (candidate is null)
                continue;

            var key = (candidate.RepoOwner, candidate.RepoName, candidate.PullRequestNumber);
            if (!seen.Add(key))
                continue;

            var fullRepo = $"{candidate.RepoOwner}/{candidate.RepoName}";

            if (query.RepoBlockList.Count > 0 &&
                query.RepoBlockList.Any(r => string.Equals(r, fullRepo, StringComparison.OrdinalIgnoreCase)))
                continue;

            results.Add(candidate);
            addedFromThisCall++;
        }
    }

    private static string ClassifyHttpError(HttpResponseMessage response, string body)
    {
        int code = (int)response.StatusCode;

        if (response.Headers.Contains("Retry-After"))
            return $"SecondaryRateLimit: {code} - Retry-After: {response.Headers.GetValues("Retry-After").FirstOrDefault()}";

        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var rem) &&
            int.TryParse(rem.FirstOrDefault(), out var remaining) && remaining == 0)
            return $"RateLimit: {code} - quota exhausted";

        string? ghMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msgEl))
                ghMessage = msgEl.GetString();
        }
        catch { /* ignore parse errors */ }

        return code switch
        {
            401 => $"AuthFailure: 401 - {ghMessage ?? "invalid or missing token"}",
            403 => $"IpBlockOrAbuse: 403 - {ghMessage ?? "access denied"}",
            404 => $"NotFound: 404 - {ghMessage ?? "resource not found"}",
            422 => $"QueryError: 422 - {ghMessage ?? "unprocessable query"}",
            429 => $"RateLimit: 429 - {ghMessage ?? "too many requests"}",
            _   => $"HttpError: {code} - {ghMessage ?? body[..Math.Min(100, body.Length)]}",
        };
    }

    private static string BuildRepoQuery(DiscoveryQuery query, string repoSpec)
    {
        var parts = new List<string> { "is:pr", "is:merged", $"repo:{repoSpec}" };

        if (query.MinReviewComments > 0)
            parts.Add($"comments:>{query.MinReviewComments}");

        if (query.StartDateUtc.HasValue)
            parts.Add($"merged:>={query.StartDateUtc.Value:yyyy-MM-dd}");

        if (query.EndDateUtc.HasValue)
            parts.Add($"merged:<={query.EndDateUtc.Value:yyyy-MM-dd}");

        return string.Join(" ", parts);
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
