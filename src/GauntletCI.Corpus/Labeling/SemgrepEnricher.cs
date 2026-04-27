// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.Text.Json;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Tier 1 scanner oracle: runs Semgrep's C# ruleset against the newly-added code
/// in each fixture's diff and writes results to the <c>semgrep_enrichments</c> table.
/// </summary>
public sealed class SemgrepEnricher
{
    private readonly string _config;

    public SemgrepEnricher(string config = "auto")
    {
        _config = config;
    }

    /// <summary>
    /// For each fixture, extracts added lines from <c>diff.patch</c>, writes them to a
    /// temporary directory, runs Semgrep over those files, and persists findings to the DB.
    /// </summary>
    public async Task<SemgrepResult> EnrichAsync(
        IReadOnlyList<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsSemgrepAvailable())
        {
            progress?.Invoke("[semgrep] WARNING: semgrep not found on PATH. Install with: pip install semgrep");
            return new SemgrepResult(0, 0, 0, SemgrepMissing: true);
        }

        int processed = 0, withFindings = 0, totalFindings = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var diffPath = Path.Combine(
                fixturesBasePath,
                fixture.Tier.ToString().ToLowerInvariant(),
                fixture.FixtureId,
                "diff.patch");

            if (!File.Exists(diffPath))
            {
                // Write a 0-finding row so CompositeLabeler knows it was checked
                await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo,
                    0, null, null, null, ct);
                processed++;
                continue;
            }

            var addedByFile = ParseAddedLinesByFile(diffPath);
            if (addedByFile.Count == 0)
            {
                await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo,
                    0, null, null, null, ct);
                processed++;
                continue;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"gauntletci_semgrep_{fixture.FixtureId}_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempDir);

                foreach (var (filePath, lines) in addedByFile)
                {
                    var safeFileName = SanitizeFileName(filePath);
                    var tempFile = Path.Combine(tempDir, safeFileName);
                    await File.WriteAllLinesAsync(tempFile, lines, ct);
                }

                var (findingCount, rulesFired, highestSeverity, findingsJson) =
                    await RunSemgrepAsync(tempDir, ct);

                await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo,
                    findingCount, rulesFired, highestSeverity, findingsJson, ct);

                processed++;
                totalFindings += findingCount;
                if (findingCount > 0)
                {
                    withFindings++;
                    progress?.Invoke(
                        $"[semgrep] {fixture.FixtureId}: {findingCount} finding(s) " +
                        $"(severity={highestSeverity}, rules={rulesFired})");
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
            }
        }

        return new SemgrepResult(processed, withFindings, totalFindings, SemgrepMissing: false);
    }

    // ── diff parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a unified diff and returns added lines grouped by .cs file path.
    /// Only files with a <c>.cs</c> extension are included.
    /// </summary>
    private static Dictionary<string, List<string>> ParseAddedLinesByFile(string diffPath)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentFile = null;

        foreach (var line in File.ReadLines(diffPath))
        {
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                var path = line[6..];
                currentFile = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? path : null;
                continue;
            }

            if (currentFile is null) continue;
            if (!line.StartsWith("+", StringComparison.Ordinal)) continue;
            if (line.StartsWith("+++", StringComparison.Ordinal)) continue;

            if (!result.TryGetValue(currentFile, out var lines))
            {
                lines = [];
                result[currentFile] = lines;
            }
            lines.Add(line[1..]); // strip leading '+'
        }

        return result;
    }

    // ── semgrep invocation ────────────────────────────────────────────────────

    private async Task<(int FindingCount, string? RulesFired, string? HighestSeverity, string? FindingsJson)>
        RunSemgrepAsync(string tempDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("semgrep",
            $"--config={_config} --json --lang=csharp --quiet {tempDir}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        string stdout;
        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start semgrep process.");
            stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return (0, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return (0, null, null, null);

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var resultsEl) ||
                resultsEl.ValueKind != JsonValueKind.Array)
                return (0, null, null, null);

            var findings = resultsEl.EnumerateArray().ToList();
            if (findings.Count == 0)
                return (0, null, null, null);

            var rules      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var severities = new List<string>();

            foreach (var f in findings)
            {
                if (f.TryGetProperty("check_id", out var cid))
                    rules.Add(cid.GetString() ?? "");

                if (f.TryGetProperty("extra", out var extra) &&
                    extra.TryGetProperty("severity", out var sev))
                    severities.Add(sev.GetString() ?? "INFO");
            }

            var highestSeverity = PickHighestSeverity(severities);
            var rulesFired      = string.Join(",", rules.Where(r => !string.IsNullOrEmpty(r)));

            return (findings.Count, rulesFired, highestSeverity, stdout);
        }
        catch
        {
            return (0, null, null, null);
        }
    }

    private static string PickHighestSeverity(IEnumerable<string> severities)
    {
        static int Rank(string s) => s.ToUpperInvariant() switch
        {
            "CRITICAL" => 5,
            "HIGH"     => 4,
            "MEDIUM"   => 3,
            "LOW"      => 2,
            "INFO"     => 1,
            _          => 0,
        };

        return severities
            .OrderByDescending(Rank)
            .FirstOrDefault() ?? "INFO";
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static bool IsSemgrepAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("semgrep", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5_000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a repo-relative file path to a safe flat filename suitable for a temp directory.
    /// e.g. "src/Foo/Bar.cs" → "src_Foo_Bar.cs"
    /// </summary>
    private static string SanitizeFileName(string filePath)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name    = filePath.Replace('/', '_').Replace('\\', '_');
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    // ── DB write ──────────────────────────────────────────────────────────────

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        int findingCount, string? rulesFired, string? highestSeverity, string? findingsJson,
        CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO semgrep_enrichments
                (fixture_id, repo, finding_count, rules_fired, highest_severity, findings_json, scanned_at_utc)
            VALUES
                ($fixtureId, $repo, $findingCount, $rulesFired, $highestSeverity, $findingsJson, datetime('now'))
            """;
        cmd.Parameters.AddWithValue("$fixtureId",       fixtureId);
        cmd.Parameters.AddWithValue("$repo",             repo);
        cmd.Parameters.AddWithValue("$findingCount",     findingCount);
        cmd.Parameters.AddWithValue("$rulesFired",       (object?)rulesFired       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$highestSeverity",  (object?)highestSeverity  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$findingsJson",     (object?)findingsJson     ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Summary statistics from a <see cref="SemgrepEnricher.EnrichAsync"/> run.</summary>
public record SemgrepResult(int FixturesProcessed, int FixturesWithFindings, int TotalFindings, bool SemgrepMissing);
