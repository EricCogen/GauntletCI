// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0049 – Float/Double Equality Comparison
/// Detects direct equality (<c>==</c> / <c>!=</c>) comparisons involving floating-point
/// literals or expressions on added lines in non-test files.
/// Floating-point arithmetic is inexact; equality comparisons almost always produce
/// surprising results due to rounding, and should use an epsilon-based approach instead.
/// </summary>
public class GCI0049_FloatDoubleEqualityComparison : RuleBase
{
    public override string Id   => "GCI0049";
    public override string Name => "Float/Double Equality Comparison";

    // Matches: == or != followed by a float/double literal (e.g. 0.0, 1.5f, 2.0d, .5F)
    private static readonly Regex FloatLiteralOnRightRegex = new(
        @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d*)[fFdDmM]?\b",
        RegexOptions.Compiled);

    // Matches: float/double literal on the left side of == or !=
    private static readonly Regex FloatLiteralOnLeftRegex = new(
        @"\b(?:[-+]?\d*\.\d+|\d+\.\d*)[fFdDmM]?\s*(?:==|!=)",
        RegexOptions.Compiled);

    // Matches: a (float) or (double) cast alongside == or !=
    private static readonly Regex FloatCastWithEqualityRegex = new(
        @"\((?:float|double)\).*(?:==|!=)|(?:==|!=).*\((?:float|double)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches: a float or double variable declaration on the same line as == or !=
    private static readonly Regex FloatTypeWithEqualityRegex = new(
        @"\b(?:float|double)\b.*(?:==|!=)|(?:==|!=).*\b(?:float|double)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            foreach (var line in file.AddedLines)
            {
                var content = line.Content;

                // Skip comment lines
                var trimmed = content.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                    continue;

                // Skip string literals — crude but effective: skip if inside a quoted region
                if (IsLikelyStringComparison(content)) continue;

                bool matches = FloatLiteralOnRightRegex.IsMatch(content)
                            || FloatLiteralOnLeftRegex.IsMatch(content)
                            || FloatCastWithEqualityRegex.IsMatch(content)
                            || FloatTypeWithEqualityRegex.IsMatch(content);

                if (!matches) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Direct equality comparison on a floating-point value — use an epsilon threshold instead",
                    evidence: $"Line {line.LineNumber}: {(trimmed.Length > 120 ? trimmed[..120] + "…" : trimmed)}",
                    whyItMatters: "Floating-point arithmetic is inexact due to binary representation. " +
                                  "Two values that are mathematically equal may differ by a tiny rounding error, " +
                                  "causing == to return false and != to return true unexpectedly.",
                    suggestedAction: "Compare with an epsilon: Math.Abs(a - b) < 1e-9. " +
                                     "For financial calculations use decimal instead of float/double.",
                    confidence: Confidence.Medium,
                    line: line));
            }
        }

        return Task.FromResult(findings);
    }

    /// <summary>
    /// Returns true when the line is likely a string comparison (e.g., <c>x == "hello"</c>).
    /// Avoids false positives where a quoted numeric string is compared with ==.
    /// </summary>
    private static bool IsLikelyStringComparison(string content)
    {
        // If there is a double-quote near the equality operator, skip it
        var eqIdx = content.IndexOf("==", StringComparison.Ordinal);
        if (eqIdx < 0) eqIdx = content.IndexOf("!=", StringComparison.Ordinal);
        if (eqIdx < 0) return false;

        var window = content[(eqIdx > 5 ? eqIdx - 5 : 0)..Math.Min(content.Length, eqIdx + 20)];
        return window.Contains('"') || window.Contains('\'');
    }
}
