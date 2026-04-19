// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Tests;

/// <summary>
/// Backward-compatible overload so existing rule tests can call
/// <c>rule.EvaluateAsync(diff, staticAnalysis)</c> without modification.
/// Runs the full <see cref="ChangedFileAnalyzer"/> pipeline so that
/// <see cref="AnalysisContext.SkippedFiles"/> and the filtered
/// <see cref="AnalysisContext.Diff"/> are correctly populated.
/// </summary>
internal static class RuleTestExtensions
{
    private static readonly ChangedFileAnalyzer FileAnalyzer = new();

    internal static Task<List<Finding>> EvaluateAsync(
        this IRule rule,
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        CancellationToken ct = default,
        string? targetFramework = null,
        SyntaxContext? syntax = null)
    {
        var allRecords      = diff.Files.Select(f => FileAnalyzer.Analyze(f)).ToList();
        var eligibleRecords = allRecords.Where(r => r.IsEligible).ToList();
        var skippedRecords  = allRecords.Where(r => !r.IsEligible).ToList();

        var eligiblePaths = eligibleRecords
            .Select(r => r.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filteredDiff = new DiffContext
        {
            RawDiff       = diff.RawDiff,
            CommitSha     = diff.CommitSha,
            CommitMessage = diff.CommitMessage,
            Files         = diff.Files.Where(f => eligiblePaths.Contains(f.NewPath)).ToList(),
        };

        var context = new AnalysisContext
        {
            EligibleFiles   = eligibleRecords,
            SkippedFiles    = skippedRecords,
            FileStatistics  = FileEligibilityStatistics.From(allRecords),
            Diff            = filteredDiff,
            StaticAnalysis  = staticAnalysis,
            Syntax          = syntax ?? staticAnalysis?.Syntax,
            TargetFramework = targetFramework ?? staticAnalysis?.TargetFramework,
        };

        return rule.EvaluateAsync(context, ct);
    }
}
