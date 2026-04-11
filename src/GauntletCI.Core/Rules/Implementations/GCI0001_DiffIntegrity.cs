// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0001 – Diff Integrity
/// Detects unrelated changes, formatting churn, and mixed scope within a single diff.
/// </summary>
public class GCI0001_DiffIntegrity : RuleBase
{
    public override string Id => "GCI0001";
    public override string Name => "Diff Integrity";

    // Patterns that suggest pure formatting/whitespace changes
    private static readonly string[] FormattingOnlyPatterns = [" ", "\t", "{", "}"];

    // Extensions that are unrelated to logic (docs, assets, configs, lock files mixed with code)
    private static readonly string[] NonCodeExtensions =
        [".md", ".txt", ".png", ".jpg", ".svg", ".json", ".xml", ".yml", ".yaml", ".csproj", ".sln", ".slnx", ".lock", ".sum"];

    private static readonly string[] CodeExtensions =
        [".cs", ".ts", ".js", ".py", ".go", ".java", ".rb", ".rs", ".cpp", ".c", ".fs"];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        CheckMixedScope(diff, findings);
        CheckExcessiveFormattingChurn(diff, findings);
        CheckLargeDiffWithNoTests(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckMixedScope(DiffContext diff, List<Finding> findings)
    {
        var hasCodeFiles = diff.Files.Any(f =>
            CodeExtensions.Any(ext => f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        var hasNonCodeFiles = diff.Files.Any(f =>
            NonCodeExtensions.Any(ext => f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        if (hasCodeFiles && hasNonCodeFiles)
        {
            var nonCodeFiles = diff.Files
                .Where(f => NonCodeExtensions.Any(ext => f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.NewPath);

            findings.Add(CreateFinding(
                summary: "Diff contains mixed scope: code and non-code files changed together.",
                evidence: $"Non-code files in diff: {string.Join(", ", nonCodeFiles)}",
                whyItMatters: "Mixed-scope diffs are harder to review and increase the risk of unintended changes slipping through.",
                suggestedAction: "Split into separate PRs: one for code changes, one for docs/config updates.",
                confidence: Confidence.Medium));
        }
    }

    private void CheckExcessiveFormattingChurn(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files.Where(f =>
            CodeExtensions.Any(ext => f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
        {
            var addedLines = file.AddedLines.ToList();
            var removedLines = file.RemovedLines.ToList();

            if (addedLines.Count == 0 && removedLines.Count == 0) continue;

            // Count lines that are whitespace-only changes
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
            .Where(f => CodeExtensions.Any(ext => f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
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
