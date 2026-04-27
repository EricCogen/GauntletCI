// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Tier 1 oracle: identifies fixtures whose PR was authored by Dependabot,
/// marking them as confirmed dependency-vulnerability-fix events.
/// Results are written to the <c>dependabot_matches</c> table.
/// </summary>
public sealed class DependabotEnricher : IDisposable
{
    private static readonly Regex DependabotTitlePattern =
        new(@"^bump\s+\S+\s+from\s+\S+\s+to\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> DependabotLogins = new(StringComparer.OrdinalIgnoreCase)
    {
        "dependabot[bot]",
        "dependabot-preview[bot]",
        "dependabot",
    };

    private readonly HttpClient _http;

    public DependabotEnricher()
    {
        var token = GitHubTokenResolver.Resolve();

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    public void Dispose() => _http.Dispose();

    public bool IsAuthenticated =>
        _http.DefaultRequestHeaders.Contains("Authorization");

    /// <summary>
    /// For each fixture, calls the GitHub PR API to check whether the PR was authored by Dependabot.
    /// Writes a row to <c>dependabot_matches</c> for every fixture processed (not just Dependabot ones)
    /// so the CompositeLabeler can distinguish "checked and not Dependabot" from "never checked".
    /// </summary>
    public async Task<DependabotEnrichmentResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        int delayMs = 200,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[dependabot] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new DependabotEnrichmentResult { AuthMissing = true };
        }

        var result = new DependabotEnrichmentResult();

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            var prInfo = await FetchPrInfoAsync(parts[0], parts[1], fixture.PullRequestNumber, ct);
            if (prInfo is null) continue;

            var (isDependabot, prTitle, authorLogin) = prInfo.Value;

            await WriteMatchAsync(db, fixture.FixtureId, fixture.Repo, fixture.PullRequestNumber,
                isDependabot, prTitle, authorLogin, ct);

            result.FixturesProcessed++;

            if (isDependabot)
            {
                result.DependabotFixtures++;
                var truncated = prTitle.Length > 60 ? prTitle[..60] + "..." : prTitle;
                progress?.Invoke($"[dependabot] {fixture.FixtureId}: DEPENDABOT_FIX ('{truncated}' by {authorLogin})");
            }

            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
        }

        return result;
    }

    private async Task<(bool IsDependabot, string Title, string AuthorLogin)?> FetchPrInfoAsync(
        string owner, string repo, int prNumber, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleEl)
                ? titleEl.GetString() ?? "" : "";

            var login = "";
            if (root.TryGetProperty("user", out var userEl) &&
                userEl.TryGetProperty("login", out var loginEl))
                login = loginEl.GetString() ?? "";

            var isDependabot =
                DependabotLogins.Contains(login) ||
                DependabotTitlePattern.IsMatch(title);

            return (isDependabot, title, login);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static async Task WriteMatchAsync(
        CorpusDb db, string fixtureId, string repo, int prNumber,
        bool isDependabot, string prTitle, string authorLogin,
        CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO dependabot_matches
                (fixture_id, repo, pr_number, is_dependabot, pr_title, author_login)
            VALUES
                ($fixtureId, $repo, $prNumber, $isDependabot, $title, $login)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",   fixtureId);
        cmd.Parameters.AddWithValue("$repo",         repo);
        cmd.Parameters.AddWithValue("$prNumber",     prNumber);
        cmd.Parameters.AddWithValue("$isDependabot", isDependabot ? 1 : 0);
        cmd.Parameters.AddWithValue("$title",        prTitle);
        cmd.Parameters.AddWithValue("$login",        authorLogin);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Summary statistics from a <see cref="DependabotEnricher.EnrichAsync"/> run.</summary>
public sealed class DependabotEnrichmentResult
{
    public bool AuthMissing        { get; set; }
    public int  FixturesProcessed  { get; set; }
    public int  DependabotFixtures { get; set; }
}
