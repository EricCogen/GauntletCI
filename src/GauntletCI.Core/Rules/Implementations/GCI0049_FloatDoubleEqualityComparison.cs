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
    // Excludes decimal (m/M) suffixes — decimal equality is exact and not a precision pitfall.
    // Both alternatives require at least one digit after the decimal point to prevent the
    // regex engine from matching "1." (backtracking) when "1.0m" appears.
    private static readonly Regex FloatLiteralOnRightRegex = new(
        @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\b",
        RegexOptions.Compiled);

    // Matches: float/double literal on the left side of == or !=
    private static readonly Regex FloatLiteralOnLeftRegex = new(
        @"\b(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\s*(?:==|!=)",
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
    /// Returns true when every equality operator on the line is adjacent to a quoted string
    /// literal — e.g. <c>name == "x"</c>. Returns false as soon as any operator is found
    /// that is NOT adjacent to a string, so a line like <c>name == "x" &amp;&amp; value == 0.0</c>
    /// is NOT suppressed (the float equality is still caught).
    /// </summary>
    private static bool IsLikelyStringComparison(string content)
    {
        bool foundAny = false;
        int searchFrom = 0;
        while (searchFrom < content.Length)
        {
            int eqIdx  = content.IndexOf("==", searchFrom, StringComparison.Ordinal);
            int neqIdx = content.IndexOf("!=", searchFrom, StringComparison.Ordinal);
            // Pick the earlier operator; if neither found, stop
            int opIdx = eqIdx >= 0 && (neqIdx < 0 || eqIdx <= neqIdx) ? eqIdx : neqIdx;
            if (opIdx < 0) break;
            foundAny = true;

            var afterOp  = content[(opIdx + 2)..].TrimStart();
            var beforeOp = content[..opIdx].TrimEnd();
            bool rightIsString = afterOp.Length  > 0 && (afterOp[0]   == '"' || afterOp[0]   == '\'');
            bool leftIsString  = beforeOp.Length > 0 && (beforeOp[^1] == '"' || beforeOp[^1] == '\'');

            if (!rightIsString && !leftIsString) return false;
            searchFrom = opIdx + 2;
        }
        return foundAny;
    }
}
