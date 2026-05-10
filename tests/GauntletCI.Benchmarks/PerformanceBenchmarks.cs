// SPDX-License-Identifier: Elastic-2.0
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;

namespace GauntletCI.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class PerformanceBenchmarks
{
    private string _smallRawDiff = null!;
    private string _mediumRawDiff = null!;
    private string _largeRawDiff = null!;
    private DiffContext _smallDiff = null!;
    private DiffContext _mediumDiff = null!;
    private DiffContext _largeDiff = null!;
    private RuleOrchestrator _orchestrator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _orchestrator = RuleOrchestrator.CreateDefault();

        // Pre-generate raw diffs outside of benchmark measurements
        _smallRawDiff = GenerateDiff(1, 5, 20);
        _mediumRawDiff = GenerateDiff(5, 20, 25);
        _largeRawDiff = GenerateDiff(20, 50, 20);

        // Pre-parse diffs for rules-only benchmarks
        _smallDiff = DiffParser.Parse(_smallRawDiff);
        _mediumDiff = DiffParser.Parse(_mediumRawDiff);
        _largeDiff = DiffParser.Parse(_largeRawDiff);
    }

    [Benchmark(Description = "Parse small diff (~100 lines)")]
    public DiffContext ParseSmallDiff()
    {
        return DiffParser.Parse(_smallRawDiff);
    }

    [Benchmark(Description = "Parse medium diff (~2500 lines)")]
    public DiffContext ParseMediumDiff()
    {
        return DiffParser.Parse(_mediumRawDiff);
    }

    [Benchmark(Description = "Parse large diff (~20k lines)")]
    public DiffContext ParseLargeDiff()
    {
        return DiffParser.Parse(_largeRawDiff);
    }

    [Benchmark(Description = "Rules only small diff (no parsing)")]
    public async Task RulesOnlySmallDiff()
    {
        await _orchestrator.RunAsync(_smallDiff);
    }

    [Benchmark(Description = "Rules only medium diff (no parsing)")]
    public async Task RulesOnlyMediumDiff()
    {
        await _orchestrator.RunAsync(_mediumDiff);
    }

    [Benchmark(Description = "Rules only large diff (no parsing)")]
    public async Task RulesOnlyLargeDiff()
    {
        await _orchestrator.RunAsync(_largeDiff);
    }

    [Benchmark(Description = "End-to-end small (parse + rules)")]
    public async Task EndToEndSmall()
    {
        var diff = DiffParser.Parse(_smallRawDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end medium (parse + rules)")]
    public async Task EndToEndMedium()
    {
        var diff = DiffParser.Parse(_mediumRawDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end large (parse + rules)")]
    public async Task EndToEndLarge()
    {
        var diff = DiffParser.Parse(_largeRawDiff);
        await _orchestrator.RunAsync(diff);
    }

    private static string GenerateDiff(int fileCount, int hunksPerFile, int linesPerHunk)
    {
        var lines = new List<string>();

        for (int f = 0; f < fileCount; f++)
        {
            var fileName = $"src/File{f}.cs";
            lines.Add($"diff --git a/{fileName} b/{fileName}");
            lines.Add($"index 1234567..abcdef00 100644");
            lines.Add($"--- a/{fileName}");
            lines.Add($"+++ b/{fileName}");

            int oldStartLine = 1;
            int newStartLine = 1;

            for (int h = 0; h < hunksPerFile; h++)
            {
                // Track actual old/new line counts for this hunk
                int oldLines = 0;
                int newLines = 0;
                var hunkBody = new List<string>();

                hunkBody.Add(" public class TestClass");
                hunkBody.Add(" {");
                oldLines++;
                newLines++;

                // Generate balanced code changes
                for (int i = 0; i < linesPerHunk - 2; i++)
                {
                    if (i % 3 == 0)
                    {
                        hunkBody.Add($"-    // Line {i}");
                        oldLines++;
                    }
                    else if (i % 3 == 1)
                    {
                        hunkBody.Add($"+    // Line {i} (updated)");
                        newLines++;
                    }
                    else
                    {
                        hunkBody.Add($" public void Method{i}()");
                        oldLines++;
                        newLines++;
                    }
                }

                hunkBody.Add(" }");
                oldLines++;
                newLines++;

                // Emit hunk header with correct line counts and non-overlapping ranges
                lines.Add($"@@ -{oldStartLine},{oldLines} +{newStartLine},{newLines} @@");
                lines.AddRange(hunkBody);

                oldStartLine += oldLines;
                newStartLine += newLines;
            }
        }

        return string.Join("\n", lines);
    }
}

