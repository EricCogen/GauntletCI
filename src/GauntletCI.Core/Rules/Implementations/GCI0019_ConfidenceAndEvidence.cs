// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0019 – Confidence and Evidence
/// Self-audit rule: flags large diffs with few findings, binary files, and tiny diffs.
/// </summary>
public class GCI0019_ConfidenceAndEvidence : RuleBase, IPostProcessor
{
    public override string Id => "GCI0019";
    public override string Name => "Confidence and Evidence";

    private static readonly string[] BinaryExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".exe", ".dll",
         ".bin", ".bmp", ".ttf", ".woff", ".woff2", ".mp3", ".mp4", ".svg"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckBinaryFiles(diff, context.SkippedFiles, findings);

        return Task.FromResult(findings);
    }

    private void CheckBinaryFiles(DiffContext diff, IReadOnlyList<ChangedFileAnalysisRecord> skippedFiles, List<Finding> findings)
    {
        // Binary files are now classified upstream and appear in skippedFiles.
        // Also check eligible diff files as a fallback (e.g. unknown binary types).
        var binaryFromSkipped = skippedFiles
            .Where(r => BinaryExtensions.Any(ext =>
                r.FilePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.FilePath)
            .ToList();

        var binaryFromDiff = diff.Files
            .Where(f => BinaryExtensions.Any(ext =>
                f.NewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.NewPath)
            .ToList();

        var allBinary = binaryFromSkipped.Concat(binaryFromDiff).Distinct().ToList();

        if (allBinary.Count == 0) return;

        findings.Add(CreateFinding(
            summary: $"{allBinary.Count} binary file(s) in diff cannot be analysed.",
            evidence: $"Binary files: {string.Join(", ", allBinary)}",
            whyItMatters: "Binary files cannot be inspected for logic, credentials, or security issues by static analysis.",
            suggestedAction: "Review binary files manually, consider storing large binaries in Git LFS.",
            confidence: Confidence.Low));
    }

    private void CheckTinyDiff(DiffContext diff, List<Finding> findings)
    {
        // Removed: single-line refactors (var→const, etc.) were generating false positives.
        // This check is intentionally disabled.
    }

    /// <summary>
    /// Called by <see cref="RuleOrchestrator"/> after all rules have run.
    /// Flags large diffs that may have hidden risks not caught by deterministic rules.
    /// </summary>
    public Finding? PostProcess(DiffContext context)
    {
        int totalLinesChanged = context.AllAddedLines.Count() + context.AllRemovedLines.Count();
        if (totalLinesChanged <= 200) return null;

        return CreateFinding(
            summary: "Large diff with few findings — hidden risks possible.",
            evidence: $"{totalLinesChanged} lines changed — deterministic rules may not catch all issues.",
            whyItMatters: "Large diffs have a higher surface area for bugs. Deterministic rules may not catch all issues.",
            suggestedAction: "Consider manual review or enabling LLM enrichment for deeper analysis.",
            confidence: Confidence.Low);
    }
}
