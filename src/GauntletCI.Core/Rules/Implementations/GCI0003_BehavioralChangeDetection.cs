// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0003 – Behavioral Change Detection
/// Detects removed logic lines and changed method signatures.
/// </summary>
public class GCI0003_BehavioralChangeDetection : RuleBase
{
    public override string Id => "GCI0003";
    public override string Name => "Behavioral Change Detection";

    private static readonly string[] LogicKeywords = ["return ", "throw ", "if (", "if(", "else", " && ", " || "];
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
        // Only count logic removals from production files — skip test and generated files.
        var removedLogicLines = diff.Files
            .Where(f => !WellKnownPatterns.IsTestFile(f.NewPath) && !WellKnownPatterns.IsGeneratedFile(f.NewPath))
            .SelectMany(f => f.RemovedLines)
            .Where(l => !l.Content.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && LogicKeywords.Any(k => l.Content.Contains(k, StringComparison.Ordinal)))
            .ToList();

        if (removedLogicLines.Count < 3) return;

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
                    if (IsBackwardCompatibleExtension(removed.Content, matchingAdded.Content))
                        compatible.Add((removedName, removed, matchingAdded));
                    else
                        incompatible.Add((removedName, removed, matchingAdded));
                }
            }

            if (incompatible.Count == 1)
            {
                var (name, removed, added) = incompatible[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"Method signature changed: '{name}' in {file.NewPath}",
                    evidence: $"Was: {removed.Content.Trim()} | Now: {added.Content.Trim()}",
                    whyItMatters: "Signature changes can break callers that haven't been updated.",
                    suggestedAction: "Verify all callers are updated and consider adding an overload for backward compatibility.",
                    confidence: Confidence.Medium,
                    line: added));
            }
            else if (incompatible.Count > 1)
            {
                var names = string.Join(", ", incompatible.Take(3).Select(c => $"'{c.Name}'"))
                    + (incompatible.Count > 3 ? $" (+{incompatible.Count - 3} more)" : "");
                var (_, firstRemoved, firstAdded) = incompatible[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"{incompatible.Count} method signatures changed (incompatible) in {file.NewPath}",
                    evidence: $"Changed: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}",
                    whyItMatters: "Signature changes can break callers that haven't been updated.",
                    suggestedAction: "Verify all callers are updated and consider adding overloads for backward compatibility.",
                    confidence: Confidence.Medium,
                    line: firstAdded));
            }

            if (compatible.Count == 1)
            {
                var (name, removed, added) = compatible[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"Backward-compatible signature extension: '{name}' in {file.NewPath}",
                    evidence: $"Was: {removed.Content.Trim()} | Now: {added.Content.Trim()}",
                    whyItMatters: "New parameters have default values (backward-compatible), but callers using positional arguments may need review.",
                    suggestedAction: "Confirm all existing callers still compile and behave correctly with the new defaults.",
                    confidence: Confidence.Low,
                    line: added));
            }
            else if (compatible.Count > 1)
            {
                var names = string.Join(", ", compatible.Take(3).Select(c => $"'{c.Name}'"))
                    + (compatible.Count > 3 ? $" (+{compatible.Count - 3} more)" : "");
                var (_, firstRemoved, firstAdded) = compatible[0];
                findings.Add(CreateFinding(
                    file,
                    summary: $"{compatible.Count} backward-compatible signature extensions in {file.NewPath}",
                    evidence: $"Extended: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}",
                    whyItMatters: "New parameters have default values (backward-compatible), but callers using positional arguments may need review.",
                    suggestedAction: "Confirm all existing callers still compile and behave correctly with the new defaults.",
                    confidence: Confidence.Low,
                    line: firstAdded));
            }
        }
    }

    /// <summary>
    /// Returns true when the only change is adding new parameters that all carry default values.
    /// Such additions are backward-compatible: existing call sites compile without modification.
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
