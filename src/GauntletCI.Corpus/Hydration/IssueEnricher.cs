// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Hydration;

/// <summary>
/// Fetches GitHub issues referenced in PR bodies and links them to fixture records in the corpus DB.
/// Requires GITHUB_TOKEN to be set; silently skips enrichment when the token is absent.
/// </summary>
public sealed class IssueEnricher : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Matches: closes #123, fixes #456, resolves owner/repo#789
    private static readonly Regex IssueRefRegex = new(
        @"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:(\w[\w-]*/[\w.-]+)?#)(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Initializes the enricher with an externally owned or injected HTTP client.
    /// </summary>
    /// <param name="http">Pre-configured HTTP client (auth headers should already be set).</param>
    /// <param name="ownsClient">When true, disposes <paramref name="http"/> on <see cref="Dispose"/>.</param>
    public IssueEnricher(HttpClient http, bool ownsClient = false)
    {
        _http = http;
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Creates a fully configured enricher using the GITHUB_TOKEN environment variable for auth.
    /// The returned instance owns its HTTP client.
    /// </summary>
    public static IssueEnricher CreateDefault()
    {
        var token = GitHubTokenResolver.Resolve();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        return new IssueEnricher(http, ownsClient: true);
    }

    public void Dispose() { if (_ownsClient) _http.Dispose(); }

    /// <summary>
    /// Parses issue references from <paramref name="prBody"/>, fetches each referenced issue from GitHub,
    /// upserts it into the corpus DB, and creates fixture-issue links.
    /// </summary>
    /// <param name="db">Open SQLite connection for the corpus database.</param>
    /// <param name="fixtureId">The fixture ID to link resolved issues against.</param>
    /// <param name="owner">Default repository owner when the issue reference omits an explicit repo.</param>
    /// <param name="repo">Default repository name when the issue reference omits an explicit repo.</param>
    /// <param name="prBody">The raw PR description body to scan for "closes #N" patterns.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of issues successfully fetched and linked.</returns>
    public async Task<int> EnrichAsync(
        SqliteConnection db, string fixtureId,
        string owner, string repo, string prBody,
        CancellationToken ct = default)
    {
        if (!GitHubTokenResolver.IsAvailable)
            return 0;

        var refs = ParseBodyRefs(owner, repo, prBody);
        if (refs.Count == 0) return 0;

        int linked = 0;
        foreach (var (issueOwner, issueRepo, issueNumber) in refs)
        {
            try
            {
                var issue = await FetchIssueAsync(issueOwner, issueRepo, issueNumber, ct).ConfigureAwait(false);
                if (issue is null) continue;
                await UpsertIssueAsync(db, issue, ct).ConfigureAwait(false);
                await LinkToFixtureAsync(db, fixtureId, issue.Id, "pr-body-ref", ct).ConfigureAwait(false);
                linked++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Issue deleted or cross-repo reference we can't resolve: skip silently
            }
        }
        return linked;
    }

    /// <summary>
    /// Extracts deduplicated issue references from a PR body using "closes/fixes/resolves #N" patterns.
    /// </summary>
    /// <param name="defaultOwner">Owner used when the reference is a bare <c>#N</c> without explicit repo.</param>
    /// <param name="defaultRepo">Repo used when the reference is a bare <c>#N</c> without explicit repo.</param>
    /// <param name="body">The PR body text to scan.</param>
    /// <returns>Deduplicated list of (owner, repo, issue number) tuples in order of appearance.</returns>
    public static List<(string Owner, string Repo, int Number)> ParseBodyRefs(
        string defaultOwner, string defaultRepo, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];
        var results = new List<(string, string, int)>();
        var seen = new HashSet<string>();

        foreach (Match m in IssueRefRegex.Matches(body))
        {
            if (!int.TryParse(m.Groups[2].Value, out var num)) continue;
            var repoRef = m.Groups[1].Value;
            string issOwner, issRepo;
            if (!string.IsNullOrEmpty(repoRef))
            {
                var parts = repoRef.Split('/');
                issOwner = parts[0]; issRepo = parts[1];
            }
            else { issOwner = defaultOwner; issRepo = defaultRepo; }

            var key = $"{issOwner}/{issRepo}#{num}";
            if (seen.Add(key)) results.Add((issOwner, issRepo, num));
        }
        return results;
    }

    private async Task<GithubIssue?> FetchIssueAsync(
        string owner, string repo, int number, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/issues/{number}";
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var gh = JsonSerializer.Deserialize<GhIssue>(json, JsonOpts);
        if (gh is null) return null;
        // Skip if it's actually a PR (GitHub issues API returns PRs too)
        if (gh.PullRequest is not null) return null;

        return new GithubIssue
        {
            Id          = $"github:{owner}/{repo}#{number}",
            RepoOwner   = owner,
            RepoName    = repo,
            Number      = number,
            Title       = gh.Title,
            Body        = gh.Body,
            Labels      = gh.Labels.Select(l => l.Name).ToList(),
            State       = gh.State,
            ClosedAtUtc = gh.ClosedAt,
            Url         = gh.HtmlUrl,
        };
    }

    private static async Task UpsertIssueAsync(SqliteConnection db, GithubIssue issue, CancellationToken ct)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO issues (id, repo_owner, repo_name, number, title, body, labels_json, state, closed_at_utc, url)
            VALUES ($id, $owner, $repo, $number, $title, $body, $labels, $state, $closedAt, $url)
            ON CONFLICT(id) DO UPDATE SET
                title=excluded.title, body=excluded.body, labels_json=excluded.labels_json,
                state=excluded.state, closed_at_utc=excluded.closed_at_utc, fetched_at_utc=datetime('now')
            """;
        cmd.Parameters.AddWithValue("$id",       issue.Id);
        cmd.Parameters.AddWithValue("$owner",    issue.RepoOwner);
        cmd.Parameters.AddWithValue("$repo",     issue.RepoName);
        cmd.Parameters.AddWithValue("$number",   issue.Number);
        cmd.Parameters.AddWithValue("$title",    issue.Title);
        cmd.Parameters.AddWithValue("$body",     (object?)issue.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$labels",   JsonSerializer.Serialize(issue.Labels));
        cmd.Parameters.AddWithValue("$state",    issue.State);
        cmd.Parameters.AddWithValue("$closedAt", issue.ClosedAtUtc.HasValue
            ? (object)issue.ClosedAtUtc.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("$url",      issue.Url);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task LinkToFixtureAsync(
        SqliteConnection db, string fixtureId, string issueId, string source, CancellationToken ct)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO fixture_issues (fixture_id, issue_id, link_source)
            VALUES ($fixtureId, $issueId, $source)
            """;
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$issueId",   issueId);
        cmd.Parameters.AddWithValue("$source",    source);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
