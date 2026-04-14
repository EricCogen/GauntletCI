// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Discovery;

public sealed class GitHubIssueDiscoveryProvider : IDiscoveryProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly string[] _labels;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] DefaultLabels = ["bug", "security", "vulnerability"];

    public GitHubIssueDiscoveryProvider(string token, string? labelsFilter = null)
    {
        _labels = string.IsNullOrWhiteSpace(labelsFilter)
            ? DefaultLabels
            : labelsFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    public void Dispose() => _http.Dispose();
    public string GetProviderName() => "gh-issues";
    public bool SupportsIncrementalSync => false;

    public async Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken cancellationToken = default)
    {
        var langFilter = query.Languages.Count > 0
            ? string.Join("+", query.Languages.Select(l => $"language:{Uri.EscapeDataString(l)}"))
            : "language:C%23";

        // OR logic: issues need only one of the labels, not all of them
        var labelFilter = _labels.Length == 1
            ? $"label:{Uri.EscapeDataString(_labels[0])}"
            : "(" + string.Join("+OR+", _labels.Select(l => $"label:{Uri.EscapeDataString(l)}")) + ")";
        var q = $"is:issue+state:closed+{labelFilter}+{langFilter}";
        var limit = query.MaxCandidates > 0 ? query.MaxCandidates : 50;

        var searchUrl = $"https://api.github.com/search/issues?q={q}&sort=comments&per_page={Math.Min(limit, 100)}&page=1";
        Console.WriteLine($"[corpus/issues] Searching: {searchUrl}");

        var issueResults = await GetJsonAsync<GhIssueSearchResult>(searchUrl, cancellationToken);
        var candidates = new List<PullRequestCandidate>();
        var seen = new HashSet<string>();

        foreach (var issue in issueResults.Items.Take(limit))
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Parse owner/repo from html_url: https://github.com/owner/repo/issues/N
            var (owner, repo, issueNumber) = TryParseIssueUrl(issue.HtmlUrl);
            if (owner is null) continue;

            var fullRepo = $"{owner}/{repo}";
            if (query.RepoBlockList.Count > 0 &&
                query.RepoBlockList.Any(b => b.Equals(fullRepo, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Find the PR that closed this issue via timeline
            var pr = await FindClosingPrAsync(owner, repo!, issueNumber, cancellationToken);
            if (pr is null) continue;

            var candidateId = $"{owner}/{repo}#{pr.Value}";
            if (!seen.Add(candidateId)) continue;

            candidates.Add(new PullRequestCandidate
            {
                Source            = "github-issue-discovery",
                RepoOwner         = owner,
                RepoName          = repo!,
                PullRequestNumber = pr.Value,
                Url               = $"https://github.com/{owner}/{repo}/pull/{pr.Value}",
                Language          = query.Languages.Count > 0 ? query.Languages[0] : "C#",
                CandidateReason   = $"Closes issue #{issueNumber}: {issue.Title}",
            });

            Console.WriteLine($"[corpus/issues] Found PR {owner}/{repo}#{pr.Value} via issue #{issueNumber}");
        }

        return candidates;
    }

    private async Task<int?> FindClosingPrAsync(
        string owner, string repo, int issueNumber, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}/timeline";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.mockingbird-preview+json"));

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var events = JsonSerializer.Deserialize<List<GhTimelineEvent>>(json, JsonOpts) ?? [];

            foreach (var ev in events)
            {
                if (!ev.Event.Equals("cross-referenced", StringComparison.OrdinalIgnoreCase)) continue;
                var linkedIssue = ev.Source?.Issue;
                if (linkedIssue?.PullRequest is null) continue;
                if (linkedIssue.PullRequest.MergedAt is null) continue;

                var prUrl = linkedIssue.PullRequest.Url;
                var parts = prUrl.Split('/');
                if (int.TryParse(parts[^1], out var prNum))
                    return (prNum);
            }
        }
        catch { /* network error, skip */ }

        return null;
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        const int MaxRetries = 6;
        var baseDelay = TimeSpan.FromSeconds(2);

        for (int attempt = 0; ; attempt++)
        {
            using var resp = await _http.GetAsync(url, ct);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<T>(json, JsonOpts)
                    ?? throw new InvalidOperationException($"Null response from {url}");
            }

            if (!IsRateLimited(resp) || attempt >= MaxRetries)
                resp.EnsureSuccessStatusCode();

            var waitTime = GetWaitTime(resp, baseDelay);
            baseDelay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * 2, 64));

            Console.Error.WriteLine(
                $"[corpus/issues] Rate limit (HTTP {(int)resp.StatusCode}) — " +
                $"attempt {attempt + 1}/{MaxRetries}, waiting {waitTime.TotalSeconds:F0}s…");

            await Task.Delay(waitTime, ct);
        }
    }

    private static bool IsRateLimited(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) return true;
        if (resp.StatusCode == HttpStatusCode.Forbidden &&
            resp.Headers.TryGetValues("x-ratelimit-remaining", out var vals) &&
            vals.FirstOrDefault() == "0")
            return true;
        return false;
    }

    private static TimeSpan GetWaitTime(HttpResponseMessage resp, TimeSpan fallback)
    {
        if (resp.Headers.RetryAfter?.Delta is { } delta)
            return delta + TimeSpan.FromSeconds(1);

        if (resp.Headers.TryGetValues("x-ratelimit-reset", out var resetVals) &&
            long.TryParse(resetVals.FirstOrDefault(), out var epoch))
        {
            var wait = DateTimeOffset.FromUnixTimeSeconds(epoch) - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            if (wait > TimeSpan.Zero) return wait;
        }

        var jitter = 1.0 + (Random.Shared.NextDouble() * 0.2 - 0.1);
        return TimeSpan.FromSeconds(fallback.TotalSeconds * jitter);
    }

    private static (string? Owner, string? Repo, int IssueNumber) TryParseIssueUrl(string htmlUrl)
    {
        try
        {
            var uri  = new Uri(htmlUrl);
            var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length >= 4 && int.TryParse(segs[3], out var num))
                return (segs[0], segs[1], num);
        }
        catch { }
        return (null, null, 0);
    }
}
