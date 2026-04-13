// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0005 – Test Coverage Relevance
/// Flags code changes without test changes, and orphaned test changes.
/// </summary>
public class GCI0005_TestCoverageRelevance : RuleBase
{
    public override string Id => "GCI0005";
    public override string Name => "Test Coverage Relevance";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        var codeFiles = diff.Files.Where(f => !IsTestFile(f.NewPath)).ToList();
        var testFiles = diff.Files.Where(f => IsTestFile(f.NewPath)).ToList();

        if (codeFiles.Count > 0 && testFiles.Count == 0)
        {
            findings.Add(CreateFinding(
                summary: "Code files changed with no test file changes.",
                evidence: $"Changed code files: {string.Join(", ", codeFiles.Select(f => f.NewPath))}",
                whyItMatters: "Untested changes increase regression risk. Reviewers cannot verify correctness without tests.",
                suggestedAction: "Add or update tests for the changed code files.",
                confidence: Confidence.Medium));
        }
        else if (testFiles.Count > 0 && codeFiles.Count == 0)
        {
            findings.Add(CreateFinding(
                summary: "Test files changed but no corresponding production code changed.",
                evidence: $"Changed test files: {string.Join(", ", testFiles.Select(f => f.NewPath))}",
                whyItMatters: "Orphaned test changes may indicate tests were written for code not yet implemented, or production code was accidentally excluded.",
                suggestedAction: "Verify the production code files are included in the diff, or explain why only tests changed.",
                confidence: Confidence.Medium));
        }

        return Task.FromResult(findings);
    }

    // Diverges intentionally from WellKnownPatterns.IsTestFile: this version also checks for /Specs/
    // and /spec/ directories and normalises backslashes, making it more precise for multi-language repos.
    private static bool IsTestFile(string path) =>
        path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').Contains("/Tests/", StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').Contains("/Specs/", StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').Contains("/spec/", StringComparison.OrdinalIgnoreCase);
}
