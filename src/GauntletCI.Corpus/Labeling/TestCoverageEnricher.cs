// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Pure-diff enricher that classifies each changed .cs file as production or test,
/// then detects test coverage gaps (production changes with no corresponding test changes).
/// Results are written to the <c>test_coverage_enrichments</c> table.
/// </summary>
public sealed class TestCoverageEnricher
{
    private static readonly string[] GeneratedSuffixes =
    [
        ".Designer.cs",
        ".g.cs",
        ".g.i.cs",
    ];

    private static readonly string[] GeneratedFileNames =
    [
        "AssemblyInfo.cs",
        "GlobalUsings.g.cs",
    ];

    private static readonly string[] TestFileSuffixes =
    [
        "Tests.cs",
        "Test.cs",
        "Specs.cs",
    ];

    private static readonly string[] TestPathSegments =
    [
        "test", "tests", "spec", "specs",
    ];

    public static async Task<TestCoverageResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        int processed = 0, gapFixtures = 0;

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
            var (prodCsCount, testCsCount) = ClassifyChangedFiles(diffLines);

            bool testCoverageGap = prodCsCount > 0 && testCsCount == 0;
            double testToProdRatio = prodCsCount == 0 ? 1.0 : (double)testCsCount / prodCsCount;

            await WriteEnrichmentAsync(
                db, fixture.FixtureId, fixture.Repo,
                prodCsCount, testCsCount, testCoverageGap, testToProdRatio, ct).ConfigureAwait(false);

            processed++;

            if (testCoverageGap)
            {
                gapFixtures++;
                progress?.Invoke(
                    $"[test-coverage] {fixture.FixtureId}: gap detected " +
                    $"(prod={prodCsCount}, test={testCsCount})");
            }
        }

        return new TestCoverageResult(processed, gapFixtures);
    }

    // ── diff parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses changed .cs file paths from diff lines and returns (prodCsCount, testCsCount).
    /// </summary>
    internal static (int ProdCsCount, int TestCsCount) ClassifyChangedFiles(IEnumerable<string> diffLines)
    {
        int prod = 0, test = 0;

        foreach (var line in diffLines)
        {
            if (!line.StartsWith("diff --git a/", StringComparison.Ordinal))
                continue;

            // "diff --git a/path/to/file.cs b/path/to/file.cs"
            var rest = line[13..]; // skip "diff --git a/"
            var spaceB = rest.IndexOf(" b/", StringComparison.Ordinal);
            if (spaceB < 0) continue;
            var path = rest[..spaceB];

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsGeneratedFile(path)) continue;

            if (IsTestFile(path))
                test++;
            else
                prod++;
        }

        return (prod, test);
    }

    /// <summary>Returns true if the file is a generated file excluded from production counts.</summary>
    internal static bool IsGeneratedFile(string path)
    {
        var fileName = Path.GetFileName(path);

        foreach (var name in GeneratedFileNames)
            if (string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase))
                return true;

        foreach (var suffix in GeneratedSuffixes)
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>Returns true if the file is a test file (by name suffix or path segment).</summary>
    internal static bool IsTestFile(string path)
    {
        var fileName = Path.GetFileName(path);

        foreach (var suffix in TestFileSuffixes)
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;

        var segments = path.Replace('\\', '/').Split('/');
        foreach (var segment in segments)
            foreach (var testSeg in TestPathSegments)
                if (string.Equals(segment, testSeg, StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    // ── DB write ──────────────────────────────────────────────────────────────

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        int prodCsCount, int testCsCount, bool testCoverageGap, double testToProdRatio,
        CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO test_coverage_enrichments
                (fixture_id, repo, prod_cs_count, test_cs_count, test_coverage_gap, test_to_prod_ratio)
            VALUES
                ($fixtureId, $repo, $prodCsCount, $testCsCount, $testCoverageGap, $testToProdRatio)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",      fixtureId);
        cmd.Parameters.AddWithValue("$repo",           repo);
        cmd.Parameters.AddWithValue("$prodCsCount",    prodCsCount);
        cmd.Parameters.AddWithValue("$testCsCount",    testCsCount);
        cmd.Parameters.AddWithValue("$testCoverageGap", testCoverageGap ? 1 : 0);
        cmd.Parameters.AddWithValue("$testToProdRatio", testToProdRatio);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Summary statistics from a <see cref="TestCoverageEnricher.EnrichAsync"/> run.</summary>
public record TestCoverageResult(int FixturesProcessed, int GapFixtures);
