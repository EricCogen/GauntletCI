// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0047 – Naming/Contract Alignment
/// Detects method renames where the new CRUD verb semantically contradicts the old verb,
/// and boolean property naming inversions.
/// </summary>
public class GCI0047_NamingContractAlignment : RuleBase
{
    public override string Id => "GCI0047";
    public override string Name => "Naming/Contract Alignment";

    private static readonly Regex MethodSignatureRegex = new(
        @"(?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed)\s+)*[\w<>\[\]?]+\s+((?:Get|Set|Add|Remove|Delete|Create|Update|Find|Fetch|Load|Save|Insert|Put|Post)(\w*))\s*\(",
        RegexOptions.Compiled);

    private static readonly HashSet<(string, string)> ContradictoryPairs = new()
    {
        ("Get",    "Delete"),  ("Delete", "Get"),
        ("Get",    "Remove"),  ("Remove", "Get"),
        ("Add",    "Remove"),  ("Remove", "Add"),
        ("Add",    "Delete"),  ("Delete", "Add"),
        ("Create", "Delete"),  ("Delete", "Create"),
        ("Insert", "Delete"),  ("Delete", "Insert"),
        ("Save",   "Delete"),  ("Delete", "Save"),
    };

    private static readonly (Regex Removed, Regex Added)[] BooleanInversionPairs =
    [
        (new Regex(@"\bIsEnabled\b",    RegexOptions.Compiled), new Regex(@"\bIsDisabled\b",   RegexOptions.Compiled)),
        (new Regex(@"\bIsDisabled\b",   RegexOptions.Compiled), new Regex(@"\bIsEnabled\b",    RegexOptions.Compiled)),
        (new Regex(@"\bIsActive\b",     RegexOptions.Compiled), new Regex(@"\bIsInactive\b",   RegexOptions.Compiled)),
        (new Regex(@"\bIsInactive\b",   RegexOptions.Compiled), new Regex(@"\bIsActive\b",     RegexOptions.Compiled)),
        (new Regex(@"\bIsValid\b",      RegexOptions.Compiled), new Regex(@"\bIsInvalid\b",    RegexOptions.Compiled)),
        (new Regex(@"\bIsInvalid\b",    RegexOptions.Compiled), new Regex(@"\bIsValid\b",      RegexOptions.Compiled)),
        (new Regex(@"\bCan(\w+)\b",     RegexOptions.Compiled), new Regex(@"\bCannot(\w+)\b",  RegexOptions.Compiled)),
        (new Regex(@"\bCannot(\w+)\b",  RegexOptions.Compiled), new Regex(@"\bCan(\w+)\b",     RegexOptions.Compiled)),
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath ?? file.OldPath ?? "")) continue;
            CheckCrudVerbContradict(file, findings);
            CheckBooleanNamingInversion(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckCrudVerbContradict(DiffFile file, List<Finding> findings)
    {
        // Extract (verb, suffix) from removed method signatures
        var removedMethods = ExtractVerbSuffixPairs(file.RemovedLines);
        if (removedMethods.Count == 0) return;

        // Extract (verb, suffix) from added method signatures
        var addedMethods = ExtractVerbSuffixPairs(file.AddedLines);
        if (addedMethods.Count == 0) return;

        // Accumulate counts per unique (removedVerb, addedVerb) pair to avoid N×M explosion.
        var pairCounts = new Dictionary<(string RemovedVerb, string AddedVerb), (int Count, string FirstSuffix)>();

        foreach (var (removedVerb, suffix) in removedMethods)
        {
            foreach (var (addedVerb, addedSuffix) in addedMethods)
            {
                if (!string.Equals(suffix, addedSuffix, StringComparison.Ordinal)) continue;
                if (!ContradictoryPairs.Contains((removedVerb, addedVerb))) continue;

                var key = (removedVerb, addedVerb);
                if (!pairCounts.TryGetValue(key, out var existing))
                    pairCounts[key] = (1, suffix);
                else
                    pairCounts[key] = (existing.Count + 1, existing.FirstSuffix);
            }
        }

        foreach (var ((removedVerb, addedVerb), (count, firstSuffix)) in pairCounts)
        {
            var countNote = count > 1 ? $" ({count} method(s))" : "";
            findings.Add(CreateFinding(
                file,
                summary: $"Contradictory method rename: {removedVerb} \u2192 {addedVerb}{countNote}",
                evidence: $"{Path.GetFileName(file.NewPath)}: {count} method(s) renamed from {removedVerb}* to {addedVerb}*; e.g. '{removedVerb}{firstSuffix}' \u2192 '{addedVerb}{firstSuffix}'",
                whyItMatters: "Renaming a method with a semantically opposite CRUD verb (e.g., Get\u2192Delete) changes the implied contract and can cause callers to misuse the API.",
                suggestedAction: "Verify the rename is intentional. If the behavior also changed, update all callers. If accidental, revert the method name.",
                confidence: Confidence.Medium,
                line: null));
        }
    }

    private void CheckBooleanNamingInversion(DiffFile file, List<Finding> findings)
    {
        var removedContent = string.Join("\n", file.RemovedLines.Select(l => l.Content));
        var addedContent   = string.Join("\n", file.AddedLines.Select(l => l.Content));

        if (string.IsNullOrEmpty(removedContent) || string.IsNullOrEmpty(addedContent)) return;

        foreach (var (removedPattern, addedPattern) in BooleanInversionPairs)
        {
            var removedMatch = removedPattern.Match(removedContent);
            if (!removedMatch.Success) continue;

            var addedMatch = addedPattern.Match(addedContent);
            if (!addedMatch.Success) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Boolean naming inversion: '{removedMatch.Value}' renamed to '{addedMatch.Value}'",
                evidence: $"{Path.GetFileName(file.NewPath)}: removed '{removedMatch.Value}', added '{addedMatch.Value}'",
                whyItMatters: "Inverting a boolean property name (IsEnabled→IsDisabled) inverts the polarity of all call sites, risking logic bugs in code that wasn't updated.",
                suggestedAction: "Prefer keeping the existing boolean name and toggling its value semantics, or do a global rename ensuring all call sites are updated.",
                confidence: Confidence.Medium));

            break; // one finding per file for boolean inversions
        }
    }

    private static List<(string Verb, string Suffix)> ExtractVerbSuffixPairs(IEnumerable<DiffLine> lines)
    {
        var result = new List<(string, string)>();
        foreach (var line in lines)
        {
            var match = MethodSignatureRegex.Match(line.Content);
            if (!match.Success) continue;

            var fullName = match.Groups[1].Value;
            var suffix   = match.Groups[2].Value;
            var verb     = fullName[..^suffix.Length];
            result.Add((verb, suffix));
        }
        return result;
    }
}
