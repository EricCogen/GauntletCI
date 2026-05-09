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
    private string _smallDiff = null!;
    private string _mediumDiff = null!;
    private string _largeDiff = null!;
    private RuleOrchestrator _orchestrator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _orchestrator = RuleOrchestrator.CreateDefault();
        _smallDiff = GenerateDiff(1, 5, 100);      // 1 file, 5 hunks, 100 lines
        _mediumDiff = GenerateDiff(5, 20, 500);    // 5 files, 20 hunks each, 500 lines
        _largeDiff = GenerateDiff(20, 50, 2000);   // 20 files, 50 hunks each, 2000 lines
    }

    [Benchmark(Description = "Parse small diff (1 file, 5 hunks)")]
    public DiffContext ParseSmallDiff()
    {
        return DiffParser.Parse(_smallDiff);
    }

    [Benchmark(Description = "Parse medium diff (5 files, 20 hunks each)")]
    public DiffContext ParseMediumDiff()
    {
        return DiffParser.Parse(_mediumDiff);
    }

    [Benchmark(Description = "Parse large diff (20 files, 50 hunks each)")]
    public DiffContext ParseLargeDiff()
    {
        return DiffParser.Parse(_largeDiff);
    }

    [Benchmark(Description = "Analyze small diff (rules execution)")]
    public async Task AnalyzeSmallDiff()
    {
        var diff = DiffParser.Parse(_smallDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "Analyze medium diff (rules execution)")]
    public async Task AnalyzeMediumDiff()
    {
        var diff = DiffParser.Parse(_mediumDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "Analyze large diff (rules execution)")]
    public async Task AnalyzeLargeDiff()
    {
        var diff = DiffParser.Parse(_largeDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end small (parse + analyze)")]
    public async Task EndToEndSmall()
    {
        var diff = DiffParser.Parse(_smallDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end medium (parse + analyze)")]
    public async Task EndToEndMedium()
    {
        var diff = DiffParser.Parse(_mediumDiff);
        await _orchestrator.RunAsync(diff);
    }

    [Benchmark(Description = "End-to-end large (parse + analyze)")]
    public async Task EndToEndLarge()
    {
        var diff = DiffParser.Parse(_largeDiff);
        await _orchestrator.RunAsync(diff);
    }

    private static string GenerateDiff(int fileCount, int hunksPerFile, int totalLines)
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

            int linesPerHunk = totalLines / (fileCount * hunksPerFile);
            for (int h = 0; h < hunksPerFile; h++)
            {
                int startLine = lineNum + 1;
                lineNum += linesPerHunk;

                lines.Add($"@@ -{startLine},10 +{startLine},11 @@");
                lines.Add(" public void Method() {");
                lines.Add("-    var oldCode = 42;");
                lines.Add("+    var newCode = 43;");
                lines.Add("+    if (newCode > 0) {");

                for (int i = 0; i < linesPerHunk - 4; i++)
                {
                    if (i % 2 == 0)
                        lines.Add($"-    // Old comment {i}");
                    else
                        lines.Add($"+    // New comment {i}");
                }

                lines.Add(" }");
            }
        }

        return string.Join("\n", lines);
    }
}
