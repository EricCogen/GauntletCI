// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0043, Nullability and Type Safety
/// Detects null-forgiving operator overuse, pragma warning disables for nullable, and unchecked as-casts.
/// Boundary with GCI0006 (Edge Case Handling): GCI0006 owns .Value access detection (nullable .Value
/// without a null guard). When an as-cast result is accessed via .Value on the same line, GCI0006 is
/// the authoritative reporter; GCI0043 suppresses its as-cast finding to avoid double-reporting.
/// </summary>
public class GCI0043_NullabilityTypeSafety : RuleBase
{
    public GCI0043_NullabilityTypeSafety(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0043";
    public override string Name => "Nullability and Type Safety";

    private static readonly string[] NullCheckPatterns =
        ["is null", "== null", "!= null", "?? ", "is not null"];

    private static bool IsNullForgivingLine(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("//")) return false;

        // Postfix null-forgiving: !. or !; or !, (not != which would be !=)
        for (int i = 0; i < content.Length - 1; i++)
        {
            if (content[i] != '!') continue;
            char next = content[i + 1];
            if (next == '.' || next == ';' || next == ',')
                return true;
        }
        return false;
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files.Where(f => !WellKnownPatterns.IsTestFile(f.NewPath)))
        {
            CheckNullForgiving(file, findings);
            CheckPragmaDisable(file, findings);
            CheckUncheckedAsCast(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckNullForgiving(DiffFile file, List<Finding> findings)
    {
        var matchingLines = file.AddedLines
            .Where(l => IsNullForgivingLine(l.Content))
            // GetValueForOption(opt)! is System.CommandLine's idiomatic pattern for
            // required options: the value is always set, so the ! is safe.
            .Where(l => !l.Content.Contains("GetValueForOption(", StringComparison.Ordinal))
            .ToList();

        if (matchingLines.Count <= 1) return;

        var evidence = matchingLines.Take(5)
            .Select(l => $"Line {l.LineNumber}: {l.Content.Trim()}");

        findings.Add(CreateFinding(
            file,
            summary: $"Null-forgiving operator (!) used {matchingLines.Count} times in {Path.GetFileName(file.NewPath)}",
            evidence: string.Join("; ", evidence),
            whyItMatters: "Excessive use of the null-forgiving operator suppresses nullable warnings and can mask NullReferenceExceptions that would have been caught at compile time.",
            suggestedAction: "Fix the root cause by ensuring values are non-null before use, or adjust nullability annotations rather than suppressing warnings.",
            confidence: Confidence.Low));
    }

    private void CheckPragmaDisable(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            if (!WellKnownPatterns.IsPragmaNullableDisable(line.Content)) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Nullable warning suppressed via #pragma warning disable",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Suppressing nullable warnings disables the compiler's null-safety guarantees and can hide NullReferenceExceptions.",
                suggestedAction: "Fix the underlying nullability issue rather than silencing the warning. Enable nullable reference types project-wide.",
                confidence: Confidence.Medium,
                line: line));
        }
    }

    private void CheckUncheckedAsCast(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        for (int i = 0; i < addedLines.Count; i++)
        {
            var content = addedLines[i].Content;
            if (!content.Contains(" as ", StringComparison.Ordinal)) continue;

            // Skip XML doc comment lines: they contain "as" in natural prose
            if (content.TrimStart().StartsWith("///")) continue;

            // Skip regular comment lines (// and /* ... */ block-comment body lines starting with *)
            if (content.TrimStart().StartsWith("//")) continue;
            if (content.TrimStart().StartsWith("*")) continue;

            // Skip "as" that appears inside a string literal (odd quote count before it)
            var asPos = content.IndexOf(" as ", StringComparison.Ordinal);
            if (IsInsideStringLiteral(content, asPos)) continue;

            // `as object` always succeeds for any non-null reference: safe, never returns null.
            var afterAs = content[(asPos + 4)..].TrimStart();
            if (afterAs.StartsWith("object", StringComparison.Ordinal) &&
                (afterAs.Length == 6 || (!char.IsLetterOrDigit(afterAs[6]) && afterAs[6] != '_')))
                continue;

            // (x as T)?.: null-conditional usage; NullReferenceException is impossible here.
            if (content[(asPos + 4)..].Contains(")?.", StringComparison.Ordinal)) continue;

            // GCI0006 (Edge Case Handling) owns .Value access detection. When the as-cast result
            // is immediately accessed via .Value on the same line, suppress here to avoid
            // double-reporting the same null-safety defect.
            if (content.Contains(".Value", StringComparison.Ordinal)) continue;

            // Check the same line and ±2 neighboring added lines for null checks
            int start = Math.Max(0, i - 2);
            int end = Math.Min(addedLines.Count - 1, i + 2);

            bool hasNullCheck = false;
            for (int j = start; j <= end; j++)
            {
                var neighbor = addedLines[j].Content;
                if (NullCheckPatterns.Any(p => neighbor.Contains(p, StringComparison.Ordinal)))
                {
                    hasNullCheck = true;
                    break;
                }
            }

            if (hasNullCheck) continue;

            findings.Add(CreateFinding(
                file,
                summary: "as-cast without null check nearby",
                evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                whyItMatters: "An as-cast returns null when the cast fails; without a null check, subsequent member access will throw NullReferenceException.",
                suggestedAction: "Add a null check (is null / != null / ??) immediately after the as-cast, or use a pattern match (is Type x) instead.",
                confidence: Confidence.Low,
                line: addedLines[i]));
        }
    }

    /// <summary>
    /// Returns true when the character at <paramref name="position"/> is inside a string literal,
    /// determined by counting unescaped double-quotes before that position.
    /// </summary>
    private static bool IsInsideStringLiteral(string content, int position)
    {
        if (position < 0) return false;
        int quoteCount = 0;
        for (int i = 0; i < position; i++)
        {
            if (content[i] == '"' && (i == 0 || content[i - 1] != '\\'))
                quoteCount++;
        }
        return quoteCount % 2 != 0;
    }
}

