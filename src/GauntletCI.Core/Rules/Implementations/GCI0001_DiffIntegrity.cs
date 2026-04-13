// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0001 – Diff Integrity
/// Detects unrelated changes, formatting churn, and mixed scope within a single diff.
/// </summary>
public class GCI0001_DiffIntegrity : RuleBase
{
    public override string Id => "GCI0001";
    public override string Name => "Diff Integrity";

    private static readonly string[] FormattingOnlyPatterns = [" ", "\t", "{", "}"];

    // Kept for CheckExcessiveFormattingChurn which still operates on eligible files
    private static readonly string[] CodeExtensions =
        [".cs", ".ts", ".js", ".py", ".go", ".java", ".rb", ".rs", ".cpp", ".c", ".fs"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckMixedScope(diff, context.SkippedFiles, findings);
        CheckExcessiveFormattingChurn(diff, findings);
        CheckLargeDiffWithNoTests(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckMixedScope(DiffContext diff, IReadOnlyList<ChangedFileAnalysisRecord> skippedFiles, List<Finding> findings)
    {
        bool hasCodeFiles = diff.Files.Count > 0;
        bool hasNonCodeFiles = skippedFiles.Any(x =>
            x.Classification is FileEligibilityClassification.KnownNonSource
                             or FileEligibilityClassification.UnknownUnsupported);

        if (hasCodeFiles && hasNonCodeFiles)
        {
            var nonCodeFilePaths = skippedFiles
                .Where(x => x.Classification is FileEligibilityClassification.KnownNonSource
                                             or FileEligibilityClassification.UnknownUnsupported)
                .Select(x => x.FilePath);

            findings.Add(CreateFinding(
                summary: "Diff contains mixed scope: code and non-code files changed together.",
                evidence: $"Non-code files in diff: {string.Join(", ", nonCodeFilePaths)}",
                whyItMatters: "Mixed-scope diffs are harder to review and increase the risk of unintended changes slipping through.",
                suggestedAction: "Split into separate PRs: one for code changes, one for docs/config updates.",
                confidence: Confidence.Medium));
        }
    }

    private void CheckExcessiveFormattingChurn(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();
            var removedLines = file.RemovedLines.ToList();

            if (addedLines.Count == 0 && removedLines.Count == 0) continue;

            int whitespaceOnlyPairs = 0;
            foreach (var added in addedLines)
            {
                if (string.IsNullOrWhiteSpace(added.Content))
                    whitespaceOnlyPairs++;
            }

            var totalChanged = addedLines.Count + removedLines.Count;
            if (totalChanged > 10 && whitespaceOnlyPairs > totalChanged * 0.4)
            {
                findings.Add(CreateFinding(
                    summary: $"Excessive whitespace/formatting churn in {file.NewPath}.",
                    evidence: $"{whitespaceOnlyPairs} of {totalChanged} changed lines are whitespace-only.",
                    whyItMatters: "Formatting noise obscures real logic changes and makes the diff harder to review.",
                    suggestedAction: "Run a formatter separately in a dedicated commit, or configure editor to match project style.",
                    confidence: Confidence.Low));
            }
        }
    }

    private void CheckLargeDiffWithNoTests(DiffContext diff, List<Finding> findings)
    {
        var codeFiles = diff.Files
            .Where(f => !IsTestFile(f.NewPath))
            .ToList();

        var testFiles = diff.Files.Where(f => IsTestFile(f.NewPath)).ToList();

        int totalAddedCodeLines = codeFiles.Sum(f => f.AddedLines.Count());

        if (totalAddedCodeLines > 50 && testFiles.Count == 0)
        {
            findings.Add(CreateFinding(
                summary: $"Large diff ({totalAddedCodeLines} added lines) with no test file changes.",
                evidence: $"{codeFiles.Count} code file(s) changed, 0 test files changed.",
                whyItMatters: "Substantial logic changes without test coverage increase regression risk.",
                suggestedAction: "Add or update tests covering the changed logic before merging.",
                confidence: Confidence.Medium));
        }
    }

    private static bool IsTestFile(string path) =>
        path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Spec", StringComparison.OrdinalIgnoreCase);
}
