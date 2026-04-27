// SPDX-License-Identifier: Elastic-2.0
using System.IO;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0004 - Breaking Change Risk
/// Detects [Obsolete] attribute added (active deprecation) or removed (guard stripped)
/// in production C# files. Corpus analysis shows that real breaking-change PRs are
/// uniformly identified by [Obsolete] transitions; broad public-API-removal heuristics
/// produced 117 FPs with 0 additional TPs.
/// </summary>
public class GCI0004_BreakingChangeRisk : RuleBase
{
    public override string Id => "GCI0004";
    public override string Name => "Breaking Change Risk";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckObsoleteAdded(diff, findings);
        CheckObsoleteRemoved(diff, findings);

        return Task.FromResult(findings);
    }

    // [Obsolete] added -- active deprecation of a public API.
    private void CheckObsoleteAdded(DiffContext diff, List<Finding> findings)
    {
        var hits = new List<(DiffFile File, List<DiffLine> Lines)>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath ?? file.OldPath ?? "")) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;

            var obsoleteAdded = file.AddedLines
                .Where(l => l.Content.Contains("[Obsolete", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (obsoleteAdded.Count > 0)
                hits.Add((file, obsoleteAdded));
        }

        if (hits.Count == 0) return;

        if (hits.Count <= 3)
        {
            foreach (var (file, lines) in hits)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"[Obsolete] added in {file.NewPath} - public API being deprecated.",
                    evidence: $"Added: {string.Join("; ", lines.Take(3).Select(l => l.Content.Trim()))}",
                    whyItMatters: "Adding [Obsolete] locks in a deprecation contract. Ensure the message names a successor and that no internal callers are silently broken.",
                    suggestedAction: "Verify the [Obsolete] message includes a migration path and check that all internal callers have been updated.",
                    confidence: Confidence.Medium));
            }
        }
        else
        {
            int total = hits.Sum(x => x.Lines.Count);
            findings.Add(CreateFinding(
                summary: $"[Obsolete] added to {total} members across {hits.Count} files.",
                evidence: $"Files: {FormatFileList(hits.Select(x => (x.File, x.Lines.Count)))}",
                whyItMatters: "Adding [Obsolete] locks in a deprecation contract. Ensure the message names a successor and that no internal callers are silently broken.",
                suggestedAction: "Verify [Obsolete] messages include migration paths and all internal callers are updated.",
                confidence: Confidence.Medium));
        }
    }

    // [Obsolete] removed -- deprecation guard may have been stripped prematurely.
    private void CheckObsoleteRemoved(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath ?? file.OldPath ?? "")) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;

            var removedObsolete = file.RemovedLines
                .Where(l => l.Content.Contains("[Obsolete", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removedObsolete.Count > 0)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"[Obsolete] attribute removed in {file.NewPath}.",
                    evidence: $"Removed: {string.Join("; ", removedObsolete.Take(3).Select(l => l.Content.Trim()))}",
                    whyItMatters: "Removing [Obsolete] may indicate unintentional removal of a deprecation guard, or premature deletion of an API still consumed externally.",
                    suggestedAction: "Confirm the member is no longer referenced and remove only after verifying downstream consumers.",
                    confidence: Confidence.Medium));
            }
        }
    }

    private static string FormatFileList(IEnumerable<(DiffFile File, int Count)> files)
    {
        var list = files.ToList();
        var preview = string.Join(", ", list.Take(3)
                        .Select(x => $"{Path.GetFileName(x.File.NewPath ?? x.File.OldPath)} ({x.Count})"));
        return preview + (list.Count > 3 ? $" (+{list.Count - 3} more files)" : "");
    }
}
