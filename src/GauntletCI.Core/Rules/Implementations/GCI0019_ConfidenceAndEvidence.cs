// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0019 – Confidence and Evidence
/// Self-audit rule: flags large diffs with few findings, binary files, and tiny diffs.
/// </summary>
public class GCI0019_ConfidenceAndEvidence : RuleBase
{
    public override string Id => "GCI0019";
    public override string Name => "Confidence and Evidence";

    private static readonly string[] BinaryExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".exe", ".dll",
         ".bin", ".bmp", ".ttf", ".woff", ".woff2", ".mp3", ".mp4", ".svg"];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        CheckBinaryFiles(diff, findings);
        CheckTinyDiff(diff, findings);
        // Note: the "large diff with few findings" check runs in RuleOrchestrator.PostProcess

        return Task.FromResult(findings);
    }

    private void CheckBinaryFiles(DiffContext diff, List<Finding> findings)
    {
        var binaryFiles = diff.Files
            .Where(f => BinaryExtensions.Any(ext =>
                f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (binaryFiles.Count == 0) return;

        findings.Add(CreateFinding(
            summary: $"{binaryFiles.Count} binary file(s) in diff cannot be analysed.",
            evidence: $"Binary files: {string.Join(", ", binaryFiles.Select(f => f.NewPath))}",
            whyItMatters: "Binary files cannot be inspected for logic, credentials, or security issues by static analysis.",
            suggestedAction: "Review binary files manually, consider storing large binaries in Git LFS.",
            confidence: Confidence.Low));
    }

    private void CheckTinyDiff(DiffContext diff, List<Finding> findings)
    {
        int totalLines = diff.AllAddedLines.Count() + diff.AllRemovedLines.Count();
        if (totalLines > 3 || totalLines == 0) return;

        findings.Add(CreateFinding(
            summary: $"Very small diff ({totalLines} total changed line(s)) — possibly incomplete.",
            evidence: $"Total changed lines: {totalLines}",
            whyItMatters: "A very small diff may indicate the wrong commit range was analysed, or the change is trivial.",
            suggestedAction: "Verify the correct diff was analysed. If intentional, this finding can be ignored.",
            confidence: Confidence.Low));
    }

    /// <summary>
    /// Called by RuleOrchestrator.PostProcess after all rules have run.
    /// </summary>
    public Finding? CreateLargeDiffWarning(int totalLinesChanged, int findingsCount)
    {
        if (totalLinesChanged <= 200 || findingsCount >= 2) return null;

        return CreateFinding(
            summary: "Large diff with few findings — hidden risks possible.",
            evidence: $"{totalLinesChanged} lines changed, {findingsCount} finding(s) from deterministic rules.",
            whyItMatters: "Large diffs have a higher surface area for bugs. Deterministic rules may not catch all issues.",
            suggestedAction: "Consider manual review or enabling LLM enrichment for deeper analysis.",
            confidence: Confidence.Low);
    }
}
