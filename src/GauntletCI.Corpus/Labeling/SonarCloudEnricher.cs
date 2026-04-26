// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Cross-references a fixture corpus against SonarCloud public project issues.
/// For each fixture, parses the changed <c>.cs</c> file paths from <c>diff.patch</c>
/// and checks whether any SonarCloud BUG or VULNERABILITY issue exists in those files.
/// Results are written to the <c>sonar_matches</c> table in the corpus database.
/// </summary>
public sealed class SonarCloudEnricher : IDisposable
{
    private readonly SonarCloudClient _client;
    private readonly Dictionary<string, string?> _projectKeyCache  = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<SonarIssue>> _issuesCache = new(StringComparer.OrdinalIgnoreCase);

    public SonarCloudEnricher()
    {
        _client = new SonarCloudClient();
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Enriches every fixture in <paramref name="fixtures"/> that has a <c>diff.patch</c>.
    /// For each unique repository, the SonarCloud project is discovered once (cached) and
    /// its issues are fetched once (cached).  Changed <c>.cs</c> file paths are extracted
    /// from the diff and matched against the cached issue list.
    /// </summary>
    public async Task<SonarEnrichmentResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        string fixturesBasePath,
        CorpusDb db,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new SonarEnrichmentResult();

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var diffPath = Path.Combine(
                fixturesBasePath,
                fixture.Tier.ToString().ToLowerInvariant(),
                fixture.FixtureId,
                "diff.patch");

            if (!File.Exists(diffPath)) continue;

            var changedFiles = ParseChangedCsFiles(diffPath);
            if (changedFiles.Count == 0) continue;

            var projectKey = await ResolveProjectKeyAsync(fixture.Repo, progress, ct);
            if (projectKey is null) continue;

            var issues = await ResolveIssuesAsync(projectKey, progress, ct);

            int matchesThisFixture = 0;
            foreach (var issue in issues)
            {
                if (!changedFiles.Contains(issue.FilePath)) continue;
                await WriteMatchAsync(db, fixture.FixtureId, issue, ct);
                matchesThisFixture++;
            }

            result.FixturesProcessed++;
            if (matchesThisFixture > 0)
            {
                result.FixturesWithMatches++;
                result.TotalMatches += matchesThisFixture;
                progress?.Invoke($"[sonarcloud] {fixture.FixtureId}: {matchesThisFixture} match(es)");
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a unified diff and returns the set of repo-relative <c>.cs</c> file paths
    /// that appear as added/modified files (<c>+++ b/...</c> lines).
    /// </summary>
    /// <remarks>
    /// SonarCloud component paths (after stripping the project key prefix) match the
    /// same repo-relative format, enabling direct set-intersection matching.
    /// </remarks>
    public static IReadOnlySet<string> ParseChangedCsFiles(string diffPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(diffPath))
        {
            if (!line.StartsWith("+++ b/", StringComparison.Ordinal)) continue;
            var path = line[6..]; // strip "+++ b/"
            if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }

        return paths;
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private async Task<string?> ResolveProjectKeyAsync(string repo, Action<string>? progress, CancellationToken ct)
    {
        if (_projectKeyCache.TryGetValue(repo, out var cached))
            return cached;

        var parts = repo.Split('/', 2);
        if (parts.Length < 2)
        {
            _projectKeyCache[repo] = null;
            return null;
        }

        progress?.Invoke($"[sonarcloud] Discovering project for {repo}...");
        var key = await _client.FindProjectKeyAsync(parts[0], parts[1], ct);
        _projectKeyCache[repo] = key;

        if (key is null)
            progress?.Invoke($"[sonarcloud] No SonarCloud project found for {repo} - skipped");
        else
            progress?.Invoke($"[sonarcloud] Found project: {key}");

        return key;
    }

    private async Task<IReadOnlyList<SonarIssue>> ResolveIssuesAsync(
        string projectKey, Action<string>? progress, CancellationToken ct)
    {
        if (_issuesCache.TryGetValue(projectKey, out var cached))
            return cached;

        progress?.Invoke($"[sonarcloud] Fetching issues for {projectKey}...");
        var issues = await _client.GetIssuesAsync(projectKey, ct);
        _issuesCache[projectKey] = issues;
        progress?.Invoke($"[sonarcloud] {issues.Count} open BUG/VULNERABILITY issue(s) for {projectKey}");

        return issues;
    }

    private static async Task WriteMatchAsync(CorpusDb db, string fixtureId, SonarIssue issue, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO sonar_matches
                (fixture_id, sonar_project_key, changed_file, sonar_rule, sonar_severity, sonar_type, sonar_message)
            VALUES
                ($fixtureId, $projectKey, $file, $rule, $severity, $type, $message)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",  fixtureId);
        cmd.Parameters.AddWithValue("$projectKey", issue.ProjectKey);
        cmd.Parameters.AddWithValue("$file",       issue.FilePath);
        cmd.Parameters.AddWithValue("$rule",       issue.Rule);
        cmd.Parameters.AddWithValue("$severity",   issue.Severity);
        cmd.Parameters.AddWithValue("$type",       issue.Type);
        cmd.Parameters.AddWithValue("$message",    issue.Message);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Summary statistics from a <see cref="SonarCloudEnricher.EnrichAsync"/> run.</summary>
public sealed class SonarEnrichmentResult
{
    public int ProjectsFound       { get; set; }
    public int ProjectsNotFound    { get; set; }
    public int FixturesProcessed   { get; set; }
    public int FixturesWithMatches { get; set; }
    public int TotalMatches        { get; set; }
}
