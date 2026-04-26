// SPDX-License-Identifier: Elastic-2.0
using System.IO;
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
        // Accumulate per-file results; cross-file dedup prevents explosion on wide diffs.
        var fileRemovals  = new List<(DiffFile File, List<(string Name, DiffLine Line)> Items)>();
        var fileSigChanges = new List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath ?? file.OldPath ?? "")) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;

            var removedPublic = file.RemovedLines
                .Where(l => IsPublicSignature(l.Content))
                .ToList();

            if (removedPublic.Count == 0) continue;

            var addedPublicLines = file.AddedLines
                .Where(l => IsPublicSignature(l.Content))
                .ToList();

            var addedSigContent = addedPublicLines.Select(l => l.Content.Trim()).ToHashSet();
            var addedSigNames   = addedPublicLines.Select(l => ExtractMemberName(l.Content))
                                                  .Where(n => n != null).ToHashSet();

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
                    if (addedLine != null
                        && StripPropertyInitializer(removed.Content) != StripPropertyInitializer(addedLine.Content)
                        && !WellKnownPatterns.IsBackwardCompatibleExtension(removed.Content, addedLine.Content))
                        sigChanges.Add((name, removed, addedLine));
                }
            }

            if (removals.Count > 0)   fileRemovals.Add((file, removals));
            if (sigChanges.Count > 0) fileSigChanges.Add((file, sigChanges));
        }

        EmitRemovals(findings, fileRemovals);
        EmitSigChanges(findings, fileSigChanges);
    }

    private void EmitRemovals(
        List<Finding> findings,
        List<(DiffFile File, List<(string Name, DiffLine Line)> Items)> fileRemovals)
    {
        if (fileRemovals.Count == 0) return;

        if (fileRemovals.Count <= 3)
        {
            foreach (var (file, removals) in fileRemovals)
            {
                var names = FormatNames(removals.Select(r => r.Name));
                findings.Add(CreateFinding(
                    file,
                    summary: removals.Count == 1
                        ? $"Public API removed: {names} in {file.NewPath}"
                        : $"Public API removed: {removals.Count} members in {file.NewPath}",
                    evidence: removals.Count == 1
                        ? $"Removed: {removals[0].Line.Content.Trim()}"
                        : $"Removed: {names} | e.g. {removals[0].Line.Content.Trim()}",
                    whyItMatters: "Removing public members is a breaking change for any consumers of this API.",
                    suggestedAction: "Mark as [Obsolete] first and schedule removal in a future major version.",
                    confidence: Confidence.High));
            }
        }
        else
        {
            int total = fileRemovals.Sum(x => x.Items.Count);
            var fileList = FormatFileList(fileRemovals.Select(x => (x.File, x.Items.Count)));
            findings.Add(CreateFinding(
                summary: $"Public API removed: {total} members across {fileRemovals.Count} files",
                evidence: $"Files: {fileList}",
                whyItMatters: "Removing public members is a breaking change for any consumers of this API.",
                suggestedAction: "Mark members as [Obsolete] first and schedule removal in a future major version.",
                confidence: Confidence.High));
        }
    }

    private void EmitSigChanges(
        List<Finding> findings,
        List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)> fileSigChanges)
    {
        if (fileSigChanges.Count == 0) return;

        if (fileSigChanges.Count <= 3)
        {
            foreach (var (file, sigChanges) in fileSigChanges)
            {
                var names = FormatNames(sigChanges.Select(c => c.Name));
                var (_, firstRemoved, firstAdded) = sigChanges[0];
                findings.Add(CreateFinding(
                    file,
                    summary: sigChanges.Count == 1
                        ? $"Public API signature changed: {names} in {file.NewPath}"
                        : $"Public API signature changed: {sigChanges.Count} members in {file.NewPath}",
                    evidence: sigChanges.Count == 1
                        ? $"Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}"
                        : $"Changed: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}",
                    whyItMatters: "Changing a public method signature is a breaking change for callers not in this diff.",
                    suggestedAction: "Provide a backward-compatible overload or bump the major version.",
                    confidence: Confidence.Medium,
                    line: firstAdded));
            }
        }
        else
        {
            int total = fileSigChanges.Sum(x => x.Items.Count);
            var fileList = FormatFileList(fileSigChanges.Select(x => (x.File, x.Items.Count)));
            findings.Add(CreateFinding(
                summary: $"Public API signature changed: {total} members across {fileSigChanges.Count} files",
                evidence: $"Files: {fileList}",
                whyItMatters: "Changing public method signatures is a breaking change for callers not in this diff.",
                suggestedAction: "Provide backward-compatible overloads or bump the major version.",
                confidence: Confidence.Medium));
        }
    }

    private static string FormatNames(IEnumerable<string> names)
    {
        var list = names.ToList();
        var preview = string.Join(", ", list.Take(3).Select(n => $"'{n}'"));
        return preview + (list.Count > 3 ? $" (+{list.Count - 3} more)" : "");
    }

    private static string FormatFileList(IEnumerable<(DiffFile File, int Count)> files)
    {
        var list = files.ToList();
        var preview = string.Join(", ", list.Take(3)
                        .Select(x => $"{Path.GetFileName(x.File.NewPath ?? x.File.OldPath)} ({x.Count})"));
        return preview + (list.Count > 3 ? $" (+{list.Count - 3} more files)" : "");
    }

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
                    evidence: $"Removed: {string.Join("; ", removedObsolete.Select(l => l.Content.Trim()))}",
                    whyItMatters: "Removing [Obsolete] may indicate unintentional removal of a deprecation guard, or premature deletion of an API still consumed externally.",
                    suggestedAction: "Confirm the member is no longer referenced and remove only after verifying downstream consumers.",
                    confidence: Confidence.Medium));
            }
        }
    }

    /// <summary>
    /// Strips a property initializer (e.g. <c>= 81920</c>) from an auto-property declaration
    /// so that a default-value-only change does not appear as a signature change.
    /// </summary>
    private static string StripPropertyInitializer(string sig)
    {
        var t = sig.Trim().TrimEnd(';');
        // Look for the closing brace of the property accessors, then strip any trailing "= ..."
        var closeBrace = t.LastIndexOf('}');
        if (closeBrace >= 0)
        {
            var rest = t[(closeBrace + 1)..].Trim();
            if (rest.StartsWith('='))
                return t[..(closeBrace + 1)].TrimEnd();
        }
        return t;
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
