// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

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
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        CheckLogicRemovedWithoutTests(diff, findings);
        CheckMethodSignatureChanges(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckLogicRemovedWithoutTests(DiffContext diff, List<Finding> findings)
    {
        var removedLogicLines = diff.AllRemovedLines
            .Where(l => LogicKeywords.Any(k => l.Content.Contains(k, StringComparison.Ordinal)))
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
                confidence: Confidence.Medium));
        }
    }

    private void CheckMethodSignatureChanges(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var removedSigs = file.RemovedLines
                .Where(l => AccessModifiers.Any(m => l.Content.Contains(m)) && l.Content.Contains('('))
                .ToList();

            var addedSigs = file.AddedLines
                .Where(l => AccessModifiers.Any(m => l.Content.Contains(m)) && l.Content.Contains('('))
                .ToList();

            foreach (var removed in removedSigs)
            {
                var removedName = ExtractMethodName(removed.Content);
                if (removedName is null) continue;

                var matchingAdded = addedSigs.FirstOrDefault(a => ExtractMethodName(a.Content) == removedName);
                if (matchingAdded is not null && NormalizeSignature(removed.Content) != NormalizeSignature(matchingAdded.Content))
                {
                    findings.Add(CreateFinding(
                        summary: $"Method signature changed: '{removedName}' in {file.NewPath}",
                        evidence: $"Was: {removed.Content.Trim()} | Now: {matchingAdded.Content.Trim()}",
                        whyItMatters: "Signature changes can break callers that haven't been updated.",
                        suggestedAction: "Verify all callers are updated and consider adding an overload for backward compatibility.",
                        confidence: Confidence.Medium));
                }
            }
        }
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
