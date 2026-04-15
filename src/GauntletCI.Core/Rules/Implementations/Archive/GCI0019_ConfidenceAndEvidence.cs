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
[ArchivedRule("Meta-rule about the engine itself; not a user-facing code risk")]
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
        // Binary files are surfaced via PostProcess(), which can inspect RawDiff (including skipped files).
        return Task.FromResult(new List<Finding>());
    }

    private void CheckTinyDiff(DiffContext diff, List<Finding> findings)
    {
        // Removed: single-line refactors (var→const, etc.) were generating false positives.
        // This check is intentionally disabled.
    }

    /// <summary>
    /// Called by <see cref="RuleOrchestrator"/> after all rules have run.
    /// </summary>
    public Finding? PostProcess(DiffContext context)
    {
        var binaryPaths = ExtractBinaryPathsFromRawDiff(context.RawDiff);
        if (binaryPaths.Count == 0) return null;

        return CreateFinding(
            summary: $"{binaryPaths.Count} binary file(s) in diff cannot be analysed.",
            evidence: $"Binary files: {string.Join(", ", binaryPaths)}",
            whyItMatters: "Binary files cannot be inspected for logic, credentials, or security issues by static analysis.",
            suggestedAction: "Review binary files manually, consider storing large binaries in Git LFS.",
            confidence: Confidence.Low);
    }

    private static List<string> ExtractBinaryPathsFromRawDiff(string rawDiff)
    {
        if (string.IsNullOrWhiteSpace(rawDiff)) return [];

        var results = new List<string>();
        string? currentNewPath = null;

        foreach (var rawLine in rawDiff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                // Format: diff --git a/path b/path
                var bIdx = line.IndexOf(" b/", StringComparison.Ordinal);
                currentNewPath = bIdx >= 0 ? line[(bIdx + 3)..].Trim() : null;
                continue;
            }

            if (line.StartsWith("Binary files ", StringComparison.Ordinal))
            {
                // Format: Binary files a/path and b/path differ
                const string marker = " and b/";
                var andIdx = line.IndexOf(marker, StringComparison.Ordinal);
                if (andIdx >= 0)
                {
                    var start = andIdx + marker.Length;
                    var end = line.IndexOf(" differ", start, StringComparison.Ordinal);
                    if (end < 0) end = line.Length;
                    var path = line[start..end].Trim();
                    if (!string.IsNullOrWhiteSpace(path)) results.Add(path);
                }
                continue;
            }

            if (line.StartsWith("GIT binary patch", StringComparison.Ordinal) && currentNewPath is not null)
            {
                if (BinaryExtensions.Any(ext => currentNewPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    results.Add(currentNewPath);
            }
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
