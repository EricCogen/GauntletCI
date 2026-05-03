// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Pure-diff enricher implementing Kamei et al. (2013) JIT defect prediction features:
/// NS (subsystems/namespaces), ND (directories), NF (files), and change entropy.
/// Results are written to the <c>diff_entropy_enrichments</c> table.
/// </summary>
public sealed class DiffEntropyEnricher
{
    public static async Task<DiffEntropyResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        int processed = 0, highEntropyFixtures = 0;

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
                processed++;
                continue;
            }

            var diffLines = await File.ReadAllLinesAsync(diffPath, ct).ConfigureAwait(false);
            var metrics   = ComputeMetrics(diffLines);

            await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo, metrics, ct).ConfigureAwait(false);

            processed++;

            if (metrics.NormalizedEntropy >= 0.8)
            {
                highEntropyFixtures++;
                progress?.Invoke(
                    $"[diff-entropy] {fixture.FixtureId}: high entropy " +
                    $"(H={metrics.ChangeEntropy:F3}, normH={metrics.NormalizedEntropy:F3}, NF={metrics.FileCount})");
            }
        }

        return new DiffEntropyResult(processed, highEntropyFixtures);
    }

    // ── diff parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Computes Kamei diff entropy metrics from diff lines.
    /// </summary>
    internal static DiffEntropyMetrics ComputeMetrics(IEnumerable<string> diffLines)
    {
        var changedLinesPerFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? currentFile = null;

        foreach (var line in diffLines)
        {
            // Detect file header
            if (line.StartsWith("diff --git a/", StringComparison.Ordinal))
            {
                var rest   = line[13..];
                var spaceB = rest.IndexOf(" b/", StringComparison.Ordinal);
                currentFile = spaceB >= 0 ? rest[..spaceB] : null;
                if (currentFile is not null && !changedLinesPerFile.ContainsKey(currentFile))
                    changedLinesPerFile[currentFile] = 0;
                continue;
            }

            if (currentFile is null) continue;

            // Count added lines
            if (line.StartsWith("+", StringComparison.Ordinal) &&
                !line.StartsWith("+++", StringComparison.Ordinal))
            {
                changedLinesPerFile[currentFile] = changedLinesPerFile.GetValueOrDefault(currentFile) + 1;
                continue;
            }

            // Count deleted lines
            if (line.StartsWith("-", StringComparison.Ordinal) &&
                !line.StartsWith("---", StringComparison.Ordinal))
            {
                changedLinesPerFile[currentFile] = changedLinesPerFile.GetValueOrDefault(currentFile) + 1;
            }
        }

        int fileCount = changedLinesPerFile.Count;
        if (fileCount == 0)
            return new DiffEntropyMetrics(0, 0, 0, 0, 0.0, 0.0);

        int totalLinesChanged = changedLinesPerFile.Values.Sum();
        int directoryCount   = CountDistinctDirectories(changedLinesPerFile.Keys);
        int namespaceCount   = CountDistinctNamespaces(changedLinesPerFile.Keys);
        double entropy       = ComputeShannonEntropy(changedLinesPerFile, totalLinesChanged);
        double normEntropy   = fileCount <= 1 ? 0.0 : entropy / Math.Log2(fileCount);

        return new DiffEntropyMetrics(
            fileCount, directoryCount, namespaceCount,
            totalLinesChanged, entropy, normEntropy);
    }

    internal static int CountDistinctDirectories(IEnumerable<string> filePaths)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in filePaths)
        {
            var normalized = path.Replace('\\', '/');
            var lastSlash  = normalized.LastIndexOf('/');
            var dir        = lastSlash >= 0 ? normalized[..lastSlash] : ".";
            dirs.Add(dir);
        }
        return dirs.Count;
    }

    internal static int CountDistinctNamespaces(IEnumerable<string> filePaths)
    {
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in filePaths)
        {
            var ns = ExtractNamespace(path);
            namespaces.Add(ns);
        }
        return namespaces.Count;
    }

    /// <summary>
    /// Estimates the top-level namespace from a file path using the first two path segments.
    /// e.g. "src/MyApp/Foo/Bar.cs" -> "src/MyApp"
    /// </summary>
    internal static string ExtractNamespace(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments   = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
            return segments[0] + "/" + segments[1];
        return segments.Length == 1 ? segments[0] : ".";
    }

    internal static double ComputeShannonEntropy(
        Dictionary<string, int> changedLinesPerFile, int totalLinesChanged)
    {
        if (totalLinesChanged == 0) return 0.0;

        double entropy = 0.0;
        foreach (var lines in changedLinesPerFile.Values)
        {
            if (lines <= 0) continue;
            double p = (double)lines / totalLinesChanged;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    // ── DB write ──────────────────────────────────────────────────────────────

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        DiffEntropyMetrics m, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO diff_entropy_enrichments
                (fixture_id, repo, file_count, directory_count, namespace_count,
                 total_lines_changed, change_entropy, normalized_entropy)
            VALUES
                ($fixtureId, $repo, $fileCount, $dirCount, $nsCount,
                 $totalLines, $entropy, $normEntropy)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",   fixtureId);
        cmd.Parameters.AddWithValue("$repo",         repo);
        cmd.Parameters.AddWithValue("$fileCount",    m.FileCount);
        cmd.Parameters.AddWithValue("$dirCount",     m.DirectoryCount);
        cmd.Parameters.AddWithValue("$nsCount",      m.NamespaceCount);
        cmd.Parameters.AddWithValue("$totalLines",   m.TotalLinesChanged);
        cmd.Parameters.AddWithValue("$entropy",      m.ChangeEntropy);
        cmd.Parameters.AddWithValue("$normEntropy",  m.NormalizedEntropy);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Intermediate metrics bag from a single diff analysis.</summary>
internal record DiffEntropyMetrics(
    int    FileCount,
    int    DirectoryCount,
    int    NamespaceCount,
    int    TotalLinesChanged,
    double ChangeEntropy,
    double NormalizedEntropy);

/// <summary>Summary statistics from a <see cref="DiffEntropyEnricher.EnrichAsync"/> run.</summary>
public record DiffEntropyResult(int FixturesProcessed, int HighEntropyFixtures);
