// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0001, Diff Integrity
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

    private static readonly HashSet<string> LockFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "packages.lock.json", "package-lock.json", "yarn.lock",
        "pnpm-lock.yaml", "Gemfile.lock", "poetry.lock",
        "Cargo.lock", "go.sum", "composer.lock",
    };

    private static bool IsLockFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return LockFileNames.Contains(Path.GetFileName(filePath));
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckMixedScope(diff, context.SkippedFiles, findings);
        CheckExcessiveFormattingChurn(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckMixedScope(DiffContext diff, IReadOnlyList<ChangedFileAnalysisRecord> skippedFiles, List<Finding> findings)
    {
        bool hasCodeFiles = diff.Files.Count > 0;

        var nonCodeFiles = skippedFiles
            .Where(x => x.Classification is FileEligibilityClassification.KnownNonSource
                                         or FileEligibilityClassification.UnknownUnsupported)
            .Where(x => !IsLockFile(x.FilePath))
            .ToList();

        if (hasCodeFiles && nonCodeFiles.Count > 0)
        {
            findings.Add(CreateFinding(
                summary: "Diff contains mixed scope: code and non-code files changed together.",
                evidence: $"Non-code files in diff: {string.Join(", ", nonCodeFiles.Select(x => x.FilePath))}",
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
                    file,
                    summary: $"Excessive whitespace/formatting churn in {file.NewPath}.",
                    evidence: $"{whitespaceOnlyPairs} of {totalChanged} changed lines are whitespace-only.",
                    whyItMatters: "Formatting noise obscures real logic changes and makes the diff harder to review.",
                    suggestedAction: "Run a formatter separately in a dedicated commit, or configure editor to match project style.",
                    confidence: Confidence.Low));
            }
        }
    }


}
