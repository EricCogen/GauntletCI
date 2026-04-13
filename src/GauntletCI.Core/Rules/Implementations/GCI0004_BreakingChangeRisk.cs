// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0004 – Breaking Change Risk
/// Detects removed public APIs and changed public method signatures.
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

        CheckRemovedPublicApi(diff, findings);
        CheckObsoleteRemoved(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckRemovedPublicApi(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var removedPublic = file.RemovedLines
                .Where(l => IsPublicSignature(l.Content))
                .ToList();

            if (removedPublic.Count == 0) continue;

            var addedSigNames = file.AddedLines
                .Where(l => IsPublicSignature(l.Content))
                .Select(l => ExtractMemberName(l.Content))
                .Where(n => n != null)
                .ToHashSet();

            foreach (var removed in removedPublic)
            {
                var name = ExtractMemberName(removed.Content);
                if (name is null) continue;

                if (!addedSigNames.Contains(name))
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Public API removed: '{name}' in {file.NewPath}",
                        evidence: $"Removed: {removed.Content.Trim()}",
                        whyItMatters: "Removing public members is a breaking change for any consumers of this API.",
                        suggestedAction: "Mark as [Obsolete] first and schedule removal in a future major version.",
                        confidence: Confidence.High));
                }
                else
                {
                    // Signature changed — compare lines directly
                    var addedLine = file.AddedLines
                        .FirstOrDefault(l => ExtractMemberName(l.Content) == name && l.Content != removed.Content);
                    if (addedLine != null)
                    {
                        findings.Add(CreateFinding(
                            file,
                            summary: $"Public API signature changed: '{name}' in {file.NewPath}",
                            evidence: $"Was: {removed.Content.Trim()} | Now: {addedLine.Content.Trim()}",
                            whyItMatters: "Changing a public method signature is a breaking change for callers not in this diff.",
                            suggestedAction: "Provide a backward-compatible overload or bump the major version.",
                            confidence: Confidence.Medium,
                            line: addedLine));
                    }
                }
            }
        }
    }

    private void CheckObsoleteRemoved(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var removedObsolete = file.RemovedLines
                .Where(l => l.Content.Contains("[Obsolete", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removedObsolete.Count > 0)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"[Obsolete] attribute removed in {file.NewPath}.",
                    evidence: $"Removed: {string.Join("; ", removedObsolete.Select(l => l.Content.Trim()))}",
                    whyItMatters: "Removing [Obsolete] may indicate unintentional removal of a deprecation guard, or premature deletion of an API still consumed externally.",
                    suggestedAction: "Confirm the member is no longer referenced and remove only after verifying downstream consumers.",
                    confidence: Confidence.Medium));
            }
        }
    }

    private static bool IsPublicSignature(string line)
    {
        var t = line.Trim();
        return t.StartsWith("public ", StringComparison.Ordinal) &&
               (t.Contains('(') || t.Contains(" class ") || t.Contains(" interface ") ||
                t.Contains(" struct ") || t.Contains(" record ") || t.Contains(" enum ") ||
                t.Contains("{ get;") || t.Contains("{ get "));
    }

    private static string? ExtractMemberName(string line)
    {
        var parenIdx = line.IndexOf('(');
        var braceIdx = line.IndexOf('{');
        var end = parenIdx >= 0 ? parenIdx : (braceIdx >= 0 ? braceIdx : -1);
        if (end <= 0) return null;
        var before = line[..end].TrimEnd();
        var lastSpace = before.LastIndexOf(' ');
        if (lastSpace < 0) return null;
        return before[(lastSpace + 1)..].Trim('(');
    }
}
