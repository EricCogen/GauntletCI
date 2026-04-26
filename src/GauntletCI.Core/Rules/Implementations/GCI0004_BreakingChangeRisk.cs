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
            if (IsTestFile(file.NewPath ?? file.OldPath)) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;

            var removedPublic = file.RemovedLines
                .Where(l => IsPublicSignature(l.Content))
                .ToList();

            if (removedPublic.Count == 0) continue;

            var addedPublicLines = file.AddedLines
                .Where(l => IsPublicSignature(l.Content))
                .ToList();

            var addedSigContent = addedPublicLines
                .Select(l => l.Content.Trim())
                .ToHashSet();

            var addedSigNames = addedPublicLines
                .Select(l => ExtractMemberName(l.Content))
                .Where(n => n != null)
                .ToHashSet();

            var removals   = new List<(string Name, DiffLine Line)>();
            var sigChanges = new List<(string Name, DiffLine RemovedLine, DiffLine AddedLine)>();

            foreach (var removed in removedPublic)
            {
                var name = ExtractMemberName(removed.Content);
                if (name is null) continue;

                if (addedSigContent.Contains(removed.Content.Trim())) continue;

                if (!addedSigNames.Contains(name))
                {
                    removals.Add((name, removed));
                }
                else
                {
                    var addedLine = addedPublicLines
                        .FirstOrDefault(l => ExtractMemberName(l.Content) == name
                                          && l.Content.Trim() != removed.Content.Trim());
                    if (addedLine != null && !IsBackwardCompatibleExtension(removed.Content, addedLine.Content))
                        sigChanges.Add((name, removed, addedLine));
                }
            }

            if (removals.Count == 1)
            {
                var (name, line) = removals[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public API removed: '{name}' in {file.NewPath}",
                    evidence: $"Removed: {line.Content.Trim()}",
                    whyItMatters: "Removing public members is a breaking change for any consumers of this API.",
                    suggestedAction: "Mark as [Obsolete] first and schedule removal in a future major version.",
                    confidence: Confidence.High));
            }
            else if (removals.Count > 1)
            {
                var names = string.Join(", ", removals.Take(3).Select(r => $"'{r.Name}'"))
                    + (removals.Count > 3 ? $" (+{removals.Count - 3} more)" : "");
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public API removed: {removals.Count} members in {file.NewPath}",
                    evidence: $"Removed: {names} | e.g. {removals[0].Line.Content.Trim()}",
                    whyItMatters: "Removing public members is a breaking change for any consumers of this API.",
                    suggestedAction: "Mark members as [Obsolete] first and schedule removal in a future major version.",
                    confidence: Confidence.High));
            }

            if (sigChanges.Count == 1)
            {
                var (name, removed, added) = sigChanges[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public API signature changed: '{name}' in {file.NewPath}",
                    evidence: $"Was: {removed.Content.Trim()} | Now: {added.Content.Trim()}",
                    whyItMatters: "Changing a public method signature is a breaking change for callers not in this diff.",
                    suggestedAction: "Provide a backward-compatible overload or bump the major version.",
                    confidence: Confidence.Medium,
                    line: added));
            }
            else if (sigChanges.Count > 1)
            {
                var names = string.Join(", ", sigChanges.Take(3).Select(c => $"'{c.Name}'"))
                    + (sigChanges.Count > 3 ? $" (+{sigChanges.Count - 3} more)" : "");
                var (_, firstRemoved, firstAdded) = sigChanges[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public API signature changed: {sigChanges.Count} members in {file.NewPath}",
                    evidence: $"Changed: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}",
                    whyItMatters: "Changing public method signatures is a breaking change for callers not in this diff.",
                    suggestedAction: "Provide backward-compatible overloads or bump the major version.",
                    confidence: Confidence.Medium,
                    line: firstAdded));
            }
        }
    }

    private void CheckObsoleteRemoved(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (IsTestFile(file.NewPath ?? file.OldPath)) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;

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

    private static bool IsTestFile(string? path)
    {
        if (path is null) return false;
        var p = path.Replace('\\', '/');
        return p.Contains("/test/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || p.Contains(".Tests/", StringComparison.OrdinalIgnoreCase)
            || p.Contains(".Test/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("/Mock", StringComparison.OrdinalIgnoreCase)
            || p.Contains("/Fake", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("Spec.cs", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("Specs.cs", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Returns true when the only change is adding new parameters that all carry default values,
    /// making the extension backward-compatible (existing call sites need no updates).
    /// </summary>
    private static bool IsBackwardCompatibleExtension(string removedSig, string addedSig)
    {
        var removedParams = ExtractParenContent(removedSig)?.Trim() ?? "";
        var addedParams   = ExtractParenContent(addedSig)?.Trim()   ?? "";

        if (addedParams.Length <= removedParams.Length) return false;
        if (!addedParams.StartsWith(removedParams, StringComparison.Ordinal)) return false;

        var extra = addedParams[removedParams.Length..].TrimStart(',').TrimStart();
        return !string.IsNullOrWhiteSpace(extra) && extra.Contains('=', StringComparison.Ordinal);
    }

    private static string? ExtractParenContent(string sig)
    {
        var open  = sig.IndexOf('(');
        var close = sig.LastIndexOf(')');
        return open >= 0 && close > open ? sig[(open + 1)..close] : null;
    }
}
