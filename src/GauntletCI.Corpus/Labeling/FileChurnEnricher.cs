// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Enricher that fetches 90-day commit frequency for each .cs file changed in a fixture diff
/// and computes a hotspot score. Results are written to the <c>file_churn_enrichments</c> table.
/// </summary>
public sealed class FileChurnEnricher : IDisposable
{
    private readonly HttpClient _http;

    public FileChurnEnricher()
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

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public async Task<FileChurnResult> EnrichAsync(
        IReadOnlyList<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        int delayMs,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[file-churn] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new FileChurnResult(0, 0, 0, AuthMissing: true);
        }

        int processed = 0, hotspotFixtures = 0, totalFilesAnalyzed = 0;
        var since90 = DateTime.UtcNow.AddDays(-90).ToString("O");

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

            var changedFiles = ParseChangedCsFiles(diffPath);
            if (changedFiles.Count == 0)
            {
                processed++;
                continue;
            }

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            bool isHotspot = false;

            foreach (var file in changedFiles)
            {
                ct.ThrowIfCancellationRequested();
                var churn = await FetchFileChurnAsync(parts[0], parts[1], file, since90, ct);
                var hotspotScore = ComputeHotspotScore(churn);

                await WriteFileChurnAsync(db, fixture.FixtureId, fixture.Repo, file, churn, hotspotScore, ct);
                totalFilesAnalyzed++;

                if (hotspotScore >= 0.7) isHotspot = true;

                if (delayMs > 0) await Task.Delay(delayMs, ct);
            }

            processed++;
            if (isHotspot)
            {
                hotspotFixtures++;
                progress?.Invoke($"[file-churn] {fixture.FixtureId}: HOTSPOT (files={changedFiles.Count})");
            }
        }

        return new FileChurnResult(processed, hotspotFixtures, totalFilesAnalyzed, AuthMissing: false);
    }

    // Public static so tests can call it directly
    public static IReadOnlyList<string> ParseChangedCsFiles(string diffPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(diffPath))
        {
            if (!line.StartsWith("+++ b/", StringComparison.Ordinal)) continue;
            var path = line[6..];
            if (!string.IsNullOrWhiteSpace(path) &&
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }
        return paths.ToList();
    }

    // Public static so tests can call it directly
    public static double ComputeHotspotScore(int churn90d) =>
        Math.Min(churn90d / 30.0, 1.0);

    private async Task<int> FetchFileChurnAsync(
        string owner, string repo, string filePath, string since, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/commits" +
                  $"?path={Uri.EscapeDataString(filePath)}&since={Uri.EscapeDataString(since)}&per_page=100";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return 0;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    private static async Task WriteFileChurnAsync(
        CorpusDb db, string fixtureId, string repo,
        string filePath, int churn90d, double hotspotScore, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO file_churn_enrichments
                (fixture_id, repo, file_path, churn_90d, hotspot_score, fetched_at_utc)
            VALUES
                ($fixtureId, $repo, $filePath, $churn, $score, datetime('now'))
            """;
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$repo",       repo);
        cmd.Parameters.AddWithValue("$filePath",   filePath);
        cmd.Parameters.AddWithValue("$churn",      churn90d);
        cmd.Parameters.AddWithValue("$score",      hotspotScore);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public record FileChurnResult(int FixturesProcessed, int HotspotFixtures, int TotalFilesAnalyzed, bool AuthMissing);
