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
    private DiffContext _smallDiff = null!;
    private DiffContext _mediumDiff = null!;
    private DiffContext _largeDiff = null!;
    private RuleOrchestrator _orchestrator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _orchestrator = RuleOrchestrator.CreateDefault();
        _smallDiff = DiffParser.Parse(GenerateDiff(1, 5, 20));      // 1 file, 5 hunks, ~100 lines
        _mediumDiff = DiffParser.Parse(GenerateDiff(5, 20, 25));    // 5 files, 20 hunks each, ~2500 lines
        _largeDiff = DiffParser.Parse(GenerateDiff(20, 50, 20));    // 20 files, 50 hunks each, ~20k lines
    }

    [Benchmark(Description = "Parse small diff (1 file, 5 hunks, ~100 lines)")]
    public DiffContext ParseSmallDiff()
    {
        var rawDiff = GenerateDiff(1, 5, 20);
        return DiffParser.Parse(rawDiff);
    }

    [Benchmark(Description = "Parse medium diff (5 files, 20 hunks, ~2500 lines)")]
    public DiffContext ParseMediumDiff()
    {
        var rawDiff = GenerateDiff(5, 20, 25);
        return DiffParser.Parse(rawDiff);
    }

    [Benchmark(Description = "Parse large diff (20 files, 50 hunks, ~20k lines)")]
    public DiffContext ParseLargeDiff()
    {
        var rawDiff = GenerateDiff(20, 50, 20);
        return DiffParser.Parse(rawDiff);
    }

    [Benchmark(Description = "Analyze small diff (parse + rules execution)")]
    public async Task AnalyzeSmallDiff()
    {
        await _orchestrator.RunAsync(_smallDiff);
    }

    [Benchmark(Description = "Analyze medium diff (parse + rules execution)")]
    public async Task AnalyzeMediumDiff()
    {
        await _orchestrator.RunAsync(_mediumDiff);
    }

    [Benchmark(Description = "Analyze large diff (parse + rules execution)")]
    public async Task AnalyzeLargeDiff()
    {
        await _orchestrator.RunAsync(_largeDiff);
    }

    [Benchmark(Description = "End-to-end small (parse + rules, ~100 lines)")]
    public async Task EndToEndSmall()
    {
        var rawDiff = GenerateDiff(1, 5, 20);
        var diff = DiffParser.Parse(rawDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end medium (parse + rules, ~2500 lines)")]
    public async Task EndToEndMedium()
    {
        var rawDiff = GenerateDiff(5, 20, 25);
        var diff = DiffParser.Parse(rawDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end large (parse + rules, ~20k lines)")]
    public async Task EndToEndLarge()
    {
        var rawDiff = GenerateDiff(20, 50, 20);
        var diff = DiffParser.Parse(rawDiff);
        await _orchestrator.RunAsync(diff);
    }

    private static string GenerateDiff(int fileCount, int hunksPerFile, int linesPerHunk)
    {
        var lines = new List<string>();
        int lineNum = 0;

        for (int f = 0; f < fileCount; f++)
        {
            var fileName = $"src/File{f}.cs";
            lines.Add($"diff --git a/{fileName} b/{fileName}");
            lines.Add($"index 1234567..abcdefg 100644");
            lines.Add($"--- a/{fileName}");
            lines.Add($"+++ b/{fileName}");

            for (int h = 0; h < hunksPerFile; h++)
            {
                int startLine = lineNum + 1;
                lineNum += linesPerHunk;

                lines.Add($"@@ -{startLine},{linesPerHunk} +{startLine},{linesPerHunk + 1} @@");
                lines.Add(" public class TestClass");
                lines.Add(" {");

                // Generate balanced code changes
                for (int i = 0; i < linesPerHunk - 2; i++)
                {
                    if (i % 3 == 0)
                        lines.Add($"-    // Line {lineNum - linesPerHunk + i}");
                    else if (i % 3 == 1)
                        lines.Add($"+    // Line {lineNum - linesPerHunk + i} (updated)");
                    else
                        lines.Add($" public void Method{i}()");
                }

                lines.Add(" }");
            }
        }

        return string.Join("\n", lines);
    }
}

