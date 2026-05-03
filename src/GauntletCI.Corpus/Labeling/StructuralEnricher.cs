// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Tier 3 structural truth enricher: detects sensitive file paths in each diff and
/// fetches file-level commit churn from GitHub.
/// Results are written to the <c>structural_enrichments</c> table.
/// </summary>
public sealed class StructuralEnricher : IDisposable
{
    private static readonly string[] SensitivePatterns =
    [
        "auth", "oauth", "token", "secret", "password", "credential",
        "crypto", "cipher", "encrypt", "sign", "key", "cert",
        "permission", "role", "claim", "jwt", "security",
        "payment", "billing", "invoice", "financial",
    ];

    private readonly HttpClient _http = HttpClientFactory.GetGitHubClient();

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public void Dispose()
    {
        // Factory manages the HttpClient lifetime, so we don't dispose it
    }

    /// <summary>
    /// Processes each fixture: parses changed files from the diff, detects sensitive paths,
    /// fetches per-file commit churn, computes a structural risk score, and writes to the DB.
    /// </summary>
    public async Task<StructuralResult> EnrichAsync(
        IReadOnlyList<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        int delayMs,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[structural] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new StructuralResult(0, 0, AuthMissing: true);
        }

        int processed = 0, sensitivePathFixtures = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var diffPath = Path.Combine(
                fixturesBasePath,
                fixture.Tier.ToString().ToLowerInvariant(),
                fixture.FixtureId,
                "diff.patch");

            if (!File.Exists(diffPath))
                continue;

            var changedFiles = ParseChangedFiles(diffPath);
            if (changedFiles.Count == 0)
                continue;

            var sensitiveFiles = changedFiles
                .Where(f => IsSensitivePath(f))
                .ToList();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            // Fetch per-file churn for .cs files only
            var since = DateTime.UtcNow.AddDays(-30).ToString("O");
            var churnByFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in changedFiles.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                ct.ThrowIfCancellationRequested();
                var churn = await FetchFileChurnAsync(parts[0], parts[1], file, since, ct).ConfigureAwait(false);
                churnByFile[file] = churn;
                if (delayMs > 0) await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }

            var maxChurn = churnByFile.Count > 0 ? churnByFile.Values.Max() : 0;
            var score    = ComputeScore(sensitiveFiles.Count > 0, maxChurn, changedFiles.Count);

            var changedFilesJson = JsonSerializer.Serialize(changedFiles);
            await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo,
                changedFilesJson, sensitiveFiles.Count, maxChurn, score, ct).ConfigureAwait(false);

            processed++;

            if (sensitiveFiles.Count > 0)
            {
                sensitivePathFixtures++;
                progress?.Invoke(
                    $"[structural] {fixture.FixtureId}: SENSITIVE_PATH " +
                    $"(score={score:F2}, files=[{string.Join(", ", sensitiveFiles)}])");
            }
        }

        return new StructuralResult(processed, sensitivePathFixtures, AuthMissing: false);
    }

    // ── diff parsing ──────────────────────────────────────────────────────────

    private static List<string> ParseChangedFiles(string diffPath)
    {
        var paths = new List<string>();
        foreach (var line in File.ReadLines(diffPath))
        {
            if (!line.StartsWith("+++ b/", StringComparison.Ordinal)) continue;
            var path = line[6..];
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
        return paths;
    }

    private static bool IsSensitivePath(string filePath)
    {
        var lower = filePath.ToLowerInvariant();
        foreach (var pattern in SensitivePatterns)
            if (lower.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // ── score computation ─────────────────────────────────────────────────────

    private static double ComputeScore(bool hasSensitivePath, int maxChurn, int changedFileCount)
    {
        double score = 0.0;
        if (hasSensitivePath) score += 0.40;
        if (maxChurn >= 10)   score += 0.30;
        else if (maxChurn >= 5) score += 0.20;
        if (changedFileCount >= 5) score += 0.10;
        return Math.Clamp(score, 0.0, 1.0);
    }

    // ── GitHub API ────────────────────────────────────────────────────────────

    private async Task<int> FetchFileChurnAsync(
        string owner, string repo, string filePath, string since, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/commits" +
                  $"?path={Uri.EscapeDataString(filePath)}&since={Uri.EscapeDataString(since)}&per_page=100";
        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return 0;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    // ── DB write ──────────────────────────────────────────────────────────────

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        string changedFilesJson, int sensitiveFileCount, int maxChurn,
        double structuralRiskScore, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO structural_enrichments
                (fixture_id, repo, changed_files_json, sensitive_file_count,
                 max_file_churn_30d, structural_risk_score, fetched_at_utc)
            VALUES
                ($fixtureId, $repo, $changedFiles, $sensitiveCount,
                 $maxChurn, $score, datetime('now'))
            """;
        cmd.Parameters.AddWithValue("$fixtureId",      fixtureId);
        cmd.Parameters.AddWithValue("$repo",            repo);
        cmd.Parameters.AddWithValue("$changedFiles",    changedFilesJson);
        cmd.Parameters.AddWithValue("$sensitiveCount",  sensitiveFileCount);
        cmd.Parameters.AddWithValue("$maxChurn",        maxChurn);
        cmd.Parameters.AddWithValue("$score",           structuralRiskScore);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Summary statistics from a <see cref="StructuralEnricher.EnrichAsync"/> run.</summary>
public record StructuralResult(int FixturesProcessed, int SensitivePathFixtures, bool AuthMissing);
