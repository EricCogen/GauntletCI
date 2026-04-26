// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Cross-references a fixture corpus against GitHub Code Scanning (CodeQL) open alerts.
/// For each fixture, parses changed <c>.cs</c> file paths from <c>diff.patch</c> and checks
/// whether any CodeQL alert exists in those files.  Results are written to the
/// <c>code_scanning_matches</c> table in the corpus database.
/// </summary>
public sealed class CodeScanningEnricher : IDisposable
{
    private readonly CodeScanningClient _client;
    private readonly Dictionary<string, IReadOnlyList<CodeScanningAlert>> _alertCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _noScanningRepos = new(StringComparer.OrdinalIgnoreCase);

    public CodeScanningEnricher()
    {
        _client = new CodeScanningClient();
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Enriches every fixture in <paramref name="fixtures"/> that has a <c>diff.patch</c>.
    /// Per unique repo, CodeQL alerts are fetched once and cached; 404/403 repos are skipped
    /// for all subsequent fixtures in the same repo.
    /// </summary>
    public async Task<CodeScanningEnrichmentResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        string fixturesBasePath,
        CorpusDb db,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!_client.IsAuthenticated)
        {
            progress?.Invoke("[codescanning] WARNING: no GitHub token found. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new CodeScanningEnrichmentResult { AuthMissing = true };
        }

        var result = new CodeScanningEnrichmentResult();

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            if (_noScanningRepos.Contains(fixture.Repo)) continue;

            var diffPath = Path.Combine(
                fixturesBasePath,
                fixture.Tier.ToString().ToLowerInvariant(),
                fixture.FixtureId,
                "diff.patch");

            if (!File.Exists(diffPath)) continue;

            var changedFiles = ParseChangedCsFiles(diffPath);
            if (changedFiles.Count == 0) continue;

            var alerts = await ResolveAlertsAsync(fixture.Repo, result, progress, ct);
            if (alerts is null) continue; // repo has no scanning

            int matchesThisFixture = 0;
            foreach (var alert in alerts)
            {
                if (!changedFiles.Contains(alert.FilePath)) continue;
                await WriteMatchAsync(db, fixture.FixtureId, alert, ct);
                matchesThisFixture++;
            }

            result.FixturesProcessed++;
            if (matchesThisFixture > 0)
            {
                result.FixturesWithMatches++;
                result.TotalMatches += matchesThisFixture;
                progress?.Invoke($"[codescanning] {fixture.FixtureId}: {matchesThisFixture} match(es)");
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a unified diff and returns the set of repo-relative <c>.cs</c> file paths
    /// that appear as added/modified files (<c>+++ b/...</c> lines).
    /// </summary>
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

    private async Task<IReadOnlyList<CodeScanningAlert>?> ResolveAlertsAsync(
        string repo, CodeScanningEnrichmentResult result, Action<string>? progress, CancellationToken ct)
    {
        if (_alertCache.TryGetValue(repo, out var cached))
            return cached;

        progress?.Invoke($"[codescanning] Fetching CodeQL alerts for {repo}...");
        var alerts = await _client.GetAlertsAsync(repo, ct: ct);

        if (alerts.Count == 0 && !_alertCache.ContainsKey(repo))
        {
            // Could be 404 (no scanning) or genuinely 0 alerts
            _noScanningRepos.Add(repo);
            result.ReposWithoutScanning++;
            progress?.Invoke($"[codescanning] No CodeQL alerts found for {repo} (scanning may not be enabled)");
            return null;
        }

        _alertCache[repo] = alerts;
        result.ReposWithScanning++;
        progress?.Invoke($"[codescanning] {alerts.Count} open CodeQL alert(s) for {repo}");
        return alerts;
    }

    private static async Task WriteMatchAsync(
        CorpusDb db, string fixtureId, CodeScanningAlert alert, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO code_scanning_matches
                (fixture_id, repo, changed_file, codeql_rule, codeql_rule_name,
                 alert_state, tool_name, severity, start_line, message)
            VALUES
                ($fixtureId, $repo, $file, $ruleId, $ruleName,
                 $state, $toolName, $severity, $startLine, $message)
            """;
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$repo",      alert.Repo);
        cmd.Parameters.AddWithValue("$file",      alert.FilePath);
        cmd.Parameters.AddWithValue("$ruleId",    alert.RuleId);
        cmd.Parameters.AddWithValue("$ruleName",  alert.RuleName);
        cmd.Parameters.AddWithValue("$state",     alert.State);
        cmd.Parameters.AddWithValue("$toolName",  alert.ToolName);
        cmd.Parameters.AddWithValue("$severity",  alert.Severity);
        cmd.Parameters.AddWithValue("$startLine", alert.StartLine);
        cmd.Parameters.AddWithValue("$message",   alert.Message);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Summary statistics from a <see cref="CodeScanningEnricher.EnrichAsync"/> run.</summary>
public sealed class CodeScanningEnrichmentResult
{
    public bool AuthMissing          { get; set; }
    public int  ReposWithScanning    { get; set; }
    public int  ReposWithoutScanning { get; set; }
    public int  FixturesProcessed    { get; set; }
    public int  FixturesWithMatches  { get; set; }
    public int  TotalMatches         { get; set; }
}
