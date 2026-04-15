// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Hydration;

/// <summary>
/// Hydrates pull requests via the GitHub REST API.
/// Set GITHUB_TOKEN env var for authenticated requests (higher rate limits).
/// </summary>
public sealed class GitHubRestHydrator : IPullRequestHydrator, IDisposable
{
    private readonly HttpClient _http;
    private readonly RawSnapshotStore _rawStore;
    private readonly bool _ownsHttpClient;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes the hydrator with an externally owned or injected HTTP client.
    /// </summary>
    /// <param name="http">The HTTP client pre-configured with auth headers.</param>
    /// <param name="rawStore">Store used to persist raw API snapshots alongside fixtures.</param>
    /// <param name="ownsHttpClient">When true, the hydrator disposes <paramref name="http"/> on <see cref="Dispose"/>.</param>
    public GitHubRestHydrator(HttpClient http, RawSnapshotStore rawStore, bool ownsHttpClient = false)
    {
        _http = http;
        _rawStore = rawStore;
        _ownsHttpClient = ownsHttpClient;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }

    /// <summary>
    /// Creates a fully configured hydrator using the GITHUB_TOKEN environment variable for auth.
    /// The returned instance owns its HTTP client and will dispose it on <see cref="Dispose"/>.
    /// </summary>
    /// <param name="fixturesBasePath">Root directory where raw fixture snapshots are stored.</param>
    public static GitHubRestHydrator CreateDefault(string fixturesBasePath = "./data/fixtures")
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Add("Authorization", $"token {token}");

        return new GitHubRestHydrator(http, new RawSnapshotStore(fixturesBasePath), ownsHttpClient: true);
    }

    /// <summary>
    /// Parses a GitHub PR URL and hydrates it as if it were an ad-hoc candidate.
    /// Convenience wrapper around <see cref="HydrateAsync"/> for manual ingestion.
    /// </summary>
    /// <param name="url">Full GitHub pull request URL, e.g. https://github.com/owner/repo/pull/123.</param>
    public async Task<HydratedPullRequest> HydrateFromUrlAsync(string url, CancellationToken ct = default)
    {
        var (owner, repo, prNumber) = ParsePrUrl(url);
        var candidate = new PullRequestCandidate
        {
            Source = "manual",
            RepoOwner = owner,
            RepoName = repo,
            PullRequestNumber = prNumber,
            Url = url,
        };
        return await HydrateAsync(candidate, ct);
    }

    /// <summary>
    /// Fetches PR metadata, files, review comments, commits, and unified diff from GitHub,
    /// persists raw snapshots, then maps everything to a <see cref="HydratedPullRequest"/>.
    /// </summary>
    /// <param name="candidate">The pull request candidate describing the target repo and PR number.</param>
    /// <param name="ct">Cancellation token propagated to all HTTP and I/O operations.</param>
    /// <returns>A fully hydrated pull request with all changed files, review comments, and diff text.</returns>
    public async Task<HydratedPullRequest> HydrateAsync(
        PullRequestCandidate candidate, CancellationToken ct = default)
    {
        var owner    = candidate.RepoOwner;
        var repo     = candidate.RepoName;
        var prNumber = candidate.PullRequestNumber;
        var fixtureId = FixtureIdHelper.Build(owner, repo, prNumber);
        var base_    = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";

        // Fetch all parts concurrently where safe
        var prTask       = GetJsonAsync<GhPullRequest>(base_, ct);
        var filesTask    = GetJsonListAsync<GhFile>($"{base_}/files", ct);
        var commentsTask = GetJsonListAsync<GhReviewComment>($"{base_}/comments", ct);
        var commitsTask  = GetJsonListAsync<GhCommit>($"{base_}/commits", ct);

        await Task.WhenAll(prTask, filesTask, commentsTask, commitsTask);

        var pr       = prTask.Result;
        var ghFiles  = filesTask.Result;
        var ghComments = commentsTask.Result;
        var ghCommits  = commitsTask.Result;

        // Diff requires a separate Accept header — serial request
        var diffText = await GetDiffAsync(base_, ct);

        // Persist raw snapshots
        var rawPrJson       = JsonSerializer.Serialize(pr, JsonOpts);
        var rawFilesJson    = JsonSerializer.Serialize(ghFiles, JsonOpts);
        var rawCommentsJson = JsonSerializer.Serialize(ghComments, JsonOpts);

        await Task.WhenAll(
            _rawStore.SaveAsync(FixtureTier.Discovery, fixtureId, "pr.json", rawPrJson, ct),
            _rawStore.SaveAsync(FixtureTier.Discovery, fixtureId, "files.json", rawFilesJson, ct),
            _rawStore.SaveAsync(FixtureTier.Discovery, fixtureId, "review-comments.json", rawCommentsJson, ct));

        // Map to domain models
        var changedFiles = ghFiles.Select(f => new ChangedFile
        {
            Path        = f.Filename,
            Status      = f.Status,
            Additions   = f.Additions,
            Deletions   = f.Deletions,
            Patch       = f.Patch ?? "",
            IsTestFile  = TestFileClassifier.IsTestFile(f.Filename),
            LanguageHint = GuessLanguage(f.Filename),
        }).ToList();

        var reviewComments = ghComments.Select(c => new ReviewComment
        {
            Author      = c.User.Login,
            Body        = c.Body,
            Path        = c.Path,
            DiffHunk    = c.DiffHunk,
            Position    = c.Position ?? 0,
            CreatedAtUtc = c.CreatedAt,
            Url         = c.HtmlUrl,
        }).ToList();

        return new HydratedPullRequest
        {
            RepoOwner          = owner,
            RepoName           = repo,
            PullRequestNumber  = prNumber,
            Title              = pr.Title,
            Body               = pr.Body ?? "",
            BaseSha            = pr.Base.Sha,
            HeadSha            = pr.Head.Sha,
            MergeCommitSha     = pr.MergeCommitSha ?? "",
            FilesChangedCount  = pr.ChangedFiles,
            Additions          = pr.Additions,
            Deletions          = pr.Deletions,
            ChangedFiles       = changedFiles,
            ReviewComments     = reviewComments,
            Commits            = ghCommits.Select(c => c.Sha).ToList(),
            DiffText           = diffText,
            RawApiPayloadJson  = rawPrJson,
            HydratedAtUtc      = DateTime.UtcNow,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        var json = await FetchWithBackoffAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Null response from {url}");
    }

    private async Task<List<T>> GetJsonListAsync<T>(string url, CancellationToken ct)
    {
        var pagedUrl = url.Contains('?') ? $"{url}&per_page=100" : $"{url}?per_page=100";
        var json = await FetchWithBackoffAsync(() => new HttpRequestMessage(HttpMethod.Get, pagedUrl), ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? [];
    }

    private Task<string> GetDiffAsync(string prUrl, CancellationToken ct) =>
        FetchWithBackoffAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, prUrl);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));
            return req;
        }, ct);

    /// <summary>
    /// Sends an HTTP request with exponential back-off on GitHub rate-limit responses
    /// (HTTP 429 or HTTP 403 with x-ratelimit-remaining: 0).
    /// Honors the Retry-After and x-ratelimit-reset response headers when present.
    /// </summary>
    private async Task<string> FetchWithBackoffAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        const int MaxRetries = 6;
        var baseDelay = TimeSpan.FromSeconds(2);

        for (int attempt = 0; ; attempt++)
        {
            using var req  = requestFactory();
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStringAsync(ct);

            if (!IsRateLimited(resp) || attempt >= MaxRetries)
                resp.EnsureSuccessStatusCode(); // throws HttpRequestException

            var waitTime = GetWaitTime(resp, baseDelay);
            baseDelay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * 2, 64));

            Console.Error.WriteLine(
                $"[corpus] Rate limit (HTTP {(int)resp.StatusCode}) — attempt {attempt + 1}/{MaxRetries}, " +
                $"waiting {waitTime.TotalSeconds:F0}s before retry…");

            await Task.Delay(waitTime, ct);
        }
    }

    private static bool IsRateLimited(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) return true;

        // GitHub also returns 403 when the primary rate limit is exhausted
        if (resp.StatusCode == HttpStatusCode.Forbidden &&
            resp.Headers.TryGetValues("x-ratelimit-remaining", out var vals) &&
            vals.FirstOrDefault() == "0")
            return true;

        return false;
    }

    private static TimeSpan GetWaitTime(HttpResponseMessage resp, TimeSpan fallback)
    {
        // Standard Retry-After header (seconds or HTTP-date)
        if (resp.Headers.RetryAfter?.Delta is { } delta)
            return delta + TimeSpan.FromSeconds(1);

        // GitHub-specific: unix timestamp when the rate-limit window resets
        if (resp.Headers.TryGetValues("x-ratelimit-reset", out var resetVals) &&
            long.TryParse(resetVals.FirstOrDefault(), out var epoch))
        {
            var resetAt = DateTimeOffset.FromUnixTimeSeconds(epoch);
            var wait    = resetAt - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2); // small buffer
            if (wait > TimeSpan.Zero) return wait;
        }

        // Exponential backoff with ±10 % jitter
        var jitter = 1.0 + (Random.Shared.NextDouble() * 0.2 - 0.1);
        return TimeSpan.FromSeconds(fallback.TotalSeconds * jitter);
    }

    internal static (string Owner, string Repo, int PrNumber) ParsePrUrl(string url)
    {
        // Handles: https://github.com/owner/repo/pull/1234
        var uri  = new Uri(url.Trim());
        var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length < 4 || !string.Equals(segs[2], "pull", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Cannot parse PR URL: {url}");

        if (!int.TryParse(segs[3], out var prNumber))
            throw new ArgumentException($"Cannot parse PR URL with non-numeric PR number: {url}");

        return (segs[0], segs[1], prNumber);
    }

    private static string GuessLanguage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs"   => "C#",
            ".ts"   => "TypeScript",
            ".js"   => "JavaScript",
            ".py"   => "Python",
            ".go"   => "Go",
            ".java" => "Java",
            ".rs"   => "Rust",
            ".rb"   => "Ruby",
            _       => "",
        };
    }
}
