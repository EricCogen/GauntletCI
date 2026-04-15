// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0034 – Null-Coalescing Expansion
/// Fires when null-safe operators are added without null injection test evidence.
/// </summary>
[ArchivedRule("Pattern matching produces too many false positives; not a real bug")]
public class GCI0034_NullCoalescingExpansion : RuleBase
{
    public override string Id => "GCI0034";
    public override string Name => "Null-Coalescing Expansion";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        int nullSafeCount = diff.Files
            .Where(f => !f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                        !f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.AddedLines)
            .Count(l => l.Content.Contains("?.", StringComparison.Ordinal) ||
                        l.Content.Contains(" ?? ", StringComparison.Ordinal));

        if (nullSafeCount == 0) return Task.FromResult(findings);

        var testLines = diff.Files
            .Where(f => f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                        f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.Hunks.SelectMany(h => h.Lines))
            .Select(l => l.Content)
            .ToList();

        bool hasNullInjection = testLines.Any(line =>
            line.Contains(" null", StringComparison.Ordinal) ||
            line.Contains("(null", StringComparison.Ordinal));

        if (!hasNullInjection)
        {
            findings.Add(CreateFinding(
                summary: $"{nullSafeCount} null-coalescing or null-conditional operator(s) added without null injection test evidence in this diff.",
                evidence: $"{nullSafeCount} added null-safe operator(s) in non-test files.",
                whyItMatters: "New null guards that are never tested with null inputs may mask unexpected NullReferenceException sources. The null path itself needs test coverage.",
                suggestedAction: "Add a test that passes null for the operand guarded by ?. or ?? to verify the fallback behavior is correct.",
                confidence: Confidence.Low));
        }

        return Task.FromResult(findings);
    }
}
