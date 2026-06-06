// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0019, Confidence and Evidence
/// Flags binary files that cannot be text-scanned and warns when large diffs produce no other findings.
/// </summary>
public class GCI0019_ConfidenceAndEvidence : RuleBase, IPostProcessor
{
    private const int LargeDiffLineThreshold = 200;
    private const int LargeDiffMaxFindings = 1;

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".bmp", ".svg",
        ".pdf", ".zip", ".7z", ".gz", ".tar", ".dll", ".exe", ".so", ".dylib",
        ".woff", ".woff2", ".ttf", ".eot", ".otf",
    };

    public GCI0019_ConfidenceAndEvidence(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0019";
    public override string Name => "Confidence and Evidence";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllFiles.Where(f => !f.IsDeleted && IsBinaryFile(f)))
        {
            findings.Add(CreateFinding(
                file,
                summary: $"Binary file changed: {Path.GetFileName(file.NewPath)}",
                evidence: file.NewPath,
                whyItMatters: "Binary files cannot be scanned for credentials, logic errors, or security issues using text-based analysis.",
                suggestedAction: "Review binary files manually and consider storing large binaries in Git LFS.",
                confidence: Confidence.Low));
        }

        return Task.FromResult(findings);
    }

    public Finding? PostProcess(DiffContext context, IReadOnlyList<Finding> findings)
    {
        var totalLines = context.AllAddedLines.Count() + context.AllRemovedLines.Count();
        var otherFindings = findings.Count(f => !string.Equals(f.RuleId, Id, StringComparison.OrdinalIgnoreCase));
        if (totalLines <= LargeDiffLineThreshold || otherFindings > LargeDiffMaxFindings)
            return null;

        return CreateFinding(
            summary: $"Large diff ({totalLines} lines changed) produced no rule findings",
            evidence: $"{totalLines} total added/removed lines across {context.Files.Count} eligible file(s)",
            whyItMatters: "Large diffs with no findings may be clean, but subtle behavioral risks can evade deterministic checks.",
            suggestedAction: "Consider a manual deep review or enable LLM-assisted analysis for this change.",
            confidence: Confidence.Low);
    }

    private static bool IsBinaryFile(DiffFile file)
    {
        if (file.Hunks.Count == 0)
            return true;

        var extension = Path.GetExtension(file.NewPath);
        return !string.IsNullOrEmpty(extension) && BinaryExtensions.Contains(extension);
    }
}
