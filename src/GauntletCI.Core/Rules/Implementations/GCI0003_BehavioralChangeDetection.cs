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
                .Where(l => { var t = l.Content.TrimStart(); return AccessModifiers.Any(m => t.StartsWith(m, StringComparison.Ordinal)) && t.Contains('('); })
                .ToList();

            var addedSigs = file.AddedLines
                .Where(l => { var t = l.Content.TrimStart(); return AccessModifiers.Any(m => t.StartsWith(m, StringComparison.Ordinal)) && t.Contains('('); })
                .ToList();

            foreach (var removed in removedSigs)
            {
                // Private methods cannot break external callers — skip entirely.
                if (removed.Content.Contains("private ", StringComparison.Ordinal)) continue;

                var removedName = ExtractMethodName(removed.Content);
                if (removedName is null) continue;

                var matchingAdded = addedSigs.FirstOrDefault(a => ExtractMethodName(a.Content) == removedName);
                if (matchingAdded is not null && NormalizeSignature(removed.Content) != NormalizeSignature(matchingAdded.Content))
                {
                    bool isCompatible = IsBackwardCompatibleExtension(removed.Content, matchingAdded.Content);
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Method signature changed: '{removedName}' in {file.NewPath}",
                        evidence: $"Was: {removed.Content.Trim()} | Now: {matchingAdded.Content.Trim()}",
                        whyItMatters: isCompatible
                            ? "New parameters have default values (backward-compatible), but callers using positional arguments may need review."
                            : "Signature changes can break callers that haven't been updated.",
                        suggestedAction: isCompatible
                            ? "Confirm all existing callers still compile and behave correctly with the new defaults."
                            : "Verify all callers are updated and consider adding an overload for backward compatibility.",
                        confidence: isCompatible ? Confidence.Low : Confidence.Medium,
                        line: matchingAdded));
                }
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

    private static string NormalizeSignature(string sig) =>
        sig.Replace("async ", "", StringComparison.Ordinal).Trim();
}
