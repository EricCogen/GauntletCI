// SPDX-License-Identifier: Elastic-2.0
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
            PatchText          = diffText,
            RawApiPayloadJson  = rawPrJson,
            HydratedAtUtc      = DateTime.UtcNow,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        var json = await _http.GetStringAsync(url, ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Null response from {url}");
    }

    private async Task<List<T>> GetJsonListAsync<T>(string url, CancellationToken ct)
    {
        var pagedUrl = url.Contains('?') ? $"{url}&per_page=100" : $"{url}?per_page=100";
        var json = await _http.GetStringAsync(pagedUrl, ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? [];
    }

    private async Task<string> GetDiffAsync(string prUrl, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, prUrl);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
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
