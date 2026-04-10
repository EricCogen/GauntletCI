// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0032 – Uncaught Exception Path
/// Fires when throw new is added without Assert.Throws or Should().Throw evidence in test files.
/// </summary>
public class GCI0032_UncaughtExceptionPath : RuleBase
{
    public override string Id => "GCI0032";
    public override string Name => "Uncaught Exception Path";

    private static readonly string[] ThrowAssertions =
    [
        "Assert.Throws", ".Should().Throw", "ThrowsAsync", "ThrowsExceptionAsync", "Throws<"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        int throwCount = diff.Files
            .Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                        !f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.AddedLines)
            .Count(l => l.Content.Contains("throw new", StringComparison.Ordinal));

        if (throwCount == 0) return Task.FromResult(findings);

        var testLines = diff.Files
            .Where(f => f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                        f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.Hunks.SelectMany(h => h.Lines))
            .Select(l => l.Content)
            .ToList();

        bool hasThrowAssertions = testLines.Any(line =>
            ThrowAssertions.Any(assertion => line.Contains(assertion, StringComparison.Ordinal)));

        if (!hasThrowAssertions)
        {
            findings.Add(CreateFinding(
                summary: $"{throwCount} 'throw new' statement(s) added without Assert.Throws or Should().Throw evidence in this diff.",
                evidence: $"{throwCount} added 'throw new' statement(s) in non-test files.",
                whyItMatters: "New exception paths that are untested may crash callers silently in production when the edge case is reached.",
                suggestedAction: "Add xUnit `Assert.Throws<T>` or FluentAssertions `.Should().Throw<T>()` tests for each new exception path.",
                confidence: Confidence.Medium));
        }

        return Task.FromResult(findings);
    }
}
