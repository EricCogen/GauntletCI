// SPDX-License-Identifier: Elastic-2.0
using System.IO;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0003, Behavioral Change Detection
/// Detects removed logic lines and changed method signatures.
/// </summary>
public class GCI0003_BehavioralChangeDetection : RuleBase
{
    public override string Id => "GCI0003";
    public override string Name => "Behavioral Change Detection";

    // Narrower keyword set: "else", "&&", "||" appear in virtually every C# file so
    // counting them as "logic" drives massive false-positive rates.
    private static readonly string[] LogicKeywords = ["return ", "throw ", "if (", "if("];
    private static readonly string[] AccessModifiers = ["public ", "private ", "protected ", "internal "];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckLogicRemovedWithoutTests(diff, findings);
        CheckMethodSignatureChanges(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckLogicRemovedWithoutTests(DiffContext diff, List<Finding> findings)
    {
        // Only count logic removals from production files: skip test and generated files.
        var removedLogicLines = diff.Files
            .Where(f => !WellKnownPatterns.IsTestFile(f.NewPath) && !WellKnownPatterns.IsGeneratedFile(f.NewPath))
            .SelectMany(f => f.RemovedLines)
            .Where(l => !l.Content.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && LogicKeywords.Any(k => l.Content.Contains(k, StringComparison.Ordinal)))
            .ToList();

        // Threshold of 15: small refactors routinely remove 5-10 lines of control flow.
        // Only a large-scale logic deletion (whole method body stripped, significant function
        // rewrite) should trigger without accompanying test changes.
        if (removedLogicLines.Count < 15) return;

        bool hasTestChanges = diff.Files.Any(f =>
            f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase));

        if (!hasTestChanges)
        {
            var examples = removedLogicLines
                .Take(3)
                .Select(l => l.Content.Trim());

            findings.Add(CreateFinding(
                summary: $"{removedLogicLines.Count} logic line(s) removed with no corresponding test changes.",
                evidence: $"Removed logic: {string.Join(" | ", examples)}",
                whyItMatters: "Removing control-flow logic without updating tests may silently break behaviour that was previously covered.",
                suggestedAction: "Add or update tests to verify the removed logic paths are intentionally no longer needed.",
                confidence: Confidence.Low));
        }
    }

    private void CheckMethodSignatureChanges(DiffContext diff, List<Finding> findings)
    {
        // Accumulate per-file results; cross-file dedup prevents explosion on wide diffs.
        var fileIncompatible = new List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)>();
        var fileCompatible   = new List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            var removedSigs = file.RemovedLines
                .Where(l => { var t = l.Content.TrimStart(); return HasAccessModifier(t) && l.Content.Contains('('); })
                .ToList();

            var addedSigs = file.AddedLines
                .Where(l => { var t = l.Content.TrimStart(); return HasAccessModifier(t) && l.Content.Contains('('); })
                .ToList();

            var incompatible = new List<(string Name, DiffLine RemovedLine, DiffLine AddedLine)>();
            var compatible   = new List<(string Name, DiffLine RemovedLine, DiffLine AddedLine)>();

            foreach (var removed in removedSigs)
            {
                if (removed.Content.TrimStart().StartsWith("private ", StringComparison.Ordinal)) continue;

                var removedName = ExtractMethodName(removed.Content);
                if (removedName is null) continue;

                var matchingAdded = addedSigs.FirstOrDefault(a => ExtractMethodName(a.Content) == removedName);
                if (matchingAdded is not null && NormalizeSignature(removed.Content) != NormalizeSignature(matchingAdded.Content))
                {
                    if (WellKnownPatterns.IsBackwardCompatibleExtension(removed.Content, matchingAdded.Content))
                        compatible.Add((removedName, removed, matchingAdded));
                    else
                        incompatible.Add((removedName, removed, matchingAdded));
                }
            }

            if (incompatible.Count > 0) fileIncompatible.Add((file, incompatible));
            if (compatible.Count > 0)   fileCompatible.Add((file, compatible));
        }

        EmitSigFindings(findings, fileIncompatible,
            single1Summary:  (name, file) => $"Method signature changed: '{name}' in {file.NewPath}",
            singleNSummary:  (count, file) => $"{count} method signatures changed (incompatible) in {file.NewPath}",
            crossSummary:    (total, fcount) => $"{total} method signatures changed (incompatible) across {fcount} files",
            whyItMatters:    "Signature changes can break callers that haven't been updated.",
            suggestedAction: "Verify all callers are updated and consider adding an overload for backward compatibility.",
            confidence:      Confidence.Medium);

        EmitSigFindings(findings, fileCompatible,
            single1Summary:  (name, file) => $"Backward-compatible signature extension: '{name}' in {file.NewPath}",
            singleNSummary:  (count, file) => $"{count} backward-compatible signature extensions in {file.NewPath}",
            crossSummary:    (total, fcount) => $"{total} backward-compatible signature extensions across {fcount} files",
            whyItMatters:    "New parameters have default values (backward-compatible), but callers using positional arguments may need review.",
            suggestedAction: "Confirm all existing callers still compile and behave correctly with the new defaults.",
            confidence:      Confidence.Low);
    }

    private void EmitSigFindings(
        List<Finding> findings,
        List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)> perFile,
        Func<string, DiffFile, string> single1Summary,
        Func<int, DiffFile, string>    singleNSummary,
        Func<int, int, string>         crossSummary,
        string whyItMatters,
        string suggestedAction,
        Confidence confidence)
    {
        if (perFile.Count == 0) return;

        if (perFile.Count <= 3)
        {
            foreach (var (file, items) in perFile)
            {
                var names = FormatNames(items.Select(c => c.Name));
                var (_, firstRemoved, firstAdded) = items[0];
                var summary = items.Count == 1
                    ? single1Summary(items[0].Name, file)
                    : singleNSummary(items.Count, file);
                var evidence = items.Count == 1
                    ? $"Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}"
                    : $"Changed: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}";
                findings.Add(CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, confidence, firstAdded));
            }
        }
        else
        {
            int total = perFile.Sum(x => x.Items.Count);
            var fileList = FormatFileList(perFile.Select(x => (x.File, x.Items.Count)));
            findings.Add(CreateFinding(
                summary:         crossSummary(total, perFile.Count),
                evidence:        $"Files: {fileList}",
                whyItMatters:    whyItMatters,
                suggestedAction: suggestedAction,
                confidence:      confidence));
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

    private static string? ExtractMethodName(string line)
    {
        var parenIdx = line.IndexOf('(');
        if (parenIdx <= 0) return null;
        var before = line[..parenIdx].TrimEnd();
        var lastSpace = before.LastIndexOf(' ');
        if (lastSpace < 0) return null;
        return before[(lastSpace + 1)..];
    }

    private static string NormalizeSignature(string sig)
    {
        var s = sig.Replace("async ", "", StringComparison.Ordinal).Trim();
        var open = s.IndexOf('(');
        if (open < 0) return s;

        // Find the matching closing paren with string-literal-aware depth tracking
        // so default values like string s = ")" don't cause early termination.
        int depth = 0;
        bool inString = false;
        char delim = '"';
        int closeIdx = -1;
        for (int i = open; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; } // skip escaped char in regular string
                if (c == delim) inString = false;
                continue;
            }
            if (c is '"' or '\'') { inString = true; delim = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') { depth--; if (depth == 0) { closeIdx = i; break; } }
        }
        if (closeIdx < 0) return s;

        // Include where-clauses (which precede the body) but drop method body (=> or {).
        for (int i = closeIdx + 1; i < s.Length; i++)
        {
            if (s[i] == '{') return s[..i].TrimEnd();
            if (s[i] == '=' && i + 1 < s.Length && s[i + 1] == '>') return s[..i].TrimEnd();
        }
        return s;
    }

    // Returns true when the trimmed line starts with a C# access modifier,
    // accounting for optional attribute prefix(es) like [Obsolete].
    private static bool HasAccessModifier(string trimmedLine)
    {
        int idx = 0;
        while (idx < trimmedLine.Length && trimmedLine[idx] == '[')
        {
            int depth = 0;
            while (idx < trimmedLine.Length)
            {
                char c = trimmedLine[idx++];
                if (c == '[') depth++;
                else if (c == ']') { if (--depth == 0) break; }
            }
            while (idx < trimmedLine.Length && trimmedLine[idx] == ' ') idx++;
        }
        var rest = trimmedLine[idx..];
        foreach (var m in AccessModifiers)
            if (rest.StartsWith(m, StringComparison.Ordinal)) return true;
        return false;
    }
}
