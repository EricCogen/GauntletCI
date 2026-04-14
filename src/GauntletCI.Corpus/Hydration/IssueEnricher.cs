// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Hydration;

public sealed class IssueEnricher : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Matches: closes #123, fixes #456, resolves owner/repo#789
    private static readonly Regex IssueRefRegex = new(
        @"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:(\w[\w-]*/[\w.-]+)?#)(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IssueEnricher(HttpClient http, bool ownsClient = false)
    {
        _http = http;
        _ownsClient = ownsClient;
    }

    public static IssueEnricher CreateDefault()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        return new IssueEnricher(http, ownsClient: true);
    }

    public void Dispose() { if (_ownsClient) _http.Dispose(); }

    public async Task<int> EnrichAsync(
        SqliteConnection db, string fixtureId,
        string owner, string repo, string prBody,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
            return 0;

        var refs = ParseBodyRefs(owner, repo, prBody);
        if (refs.Count == 0) return 0;

        int linked = 0;
        foreach (var (issueOwner, issueRepo, issueNumber) in refs)
        {
            try
            {
                var issue = await FetchIssueAsync(issueOwner, issueRepo, issueNumber, ct);
                if (issue is null) continue;
                await UpsertIssueAsync(db, issue, ct);
                await LinkToFixtureAsync(db, fixtureId, issue.Id, "pr-body-ref", ct);
                linked++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Issue deleted or cross-repo reference we can't resolve — skip silently
            }
        }
        return linked;
    }

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
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
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
        await cmd.ExecuteNonQueryAsync(ct);
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
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
