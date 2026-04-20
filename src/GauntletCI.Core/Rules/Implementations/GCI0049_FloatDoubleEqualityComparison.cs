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

    // Matches the safe-division guard pattern: integer zero-check before a ternary
    // e.g. (a + b) == 0 ? 0.0 : (double)a / (a + b)
    // In this pattern, (double)/(float) casts appear in the ternary branch, not the comparison.
    private static readonly Regex IntegerZeroGuardRegex = new(
        @"(?:==|!=)\s*0\s*\?", RegexOptions.Compiled);

    private static bool IsGuardedIntegerZeroCheck(string content) =>
        IntegerZeroGuardRegex.IsMatch(content);

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

                // Skip comment lines (simple prefix check)
                var trimmed = content.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                    continue;

                // Syntax guard: suppress if the first operator position is inside a comment or string literal.
                if (context.Syntax?.IsInCommentOrStringLiteral(
                        file.NewPath, line.LineNumber, GetFirstOperatorIndex(content)) == true)
                    continue;

                // When the line is an integer zero-guard ternary (e.g. count == 0 ? 0.0 : (double)a/b),
                // the (double)/(float) cast and the == appear in different clauses — skip cast/type checks.
                bool hasSafeDivGuard = IsGuardedIntegerZeroCheck(content);

                bool matches = HasMatchOutsideStringLiteral(FloatLiteralOnRightRegex, content)
                            || HasMatchOutsideStringLiteral(FloatLiteralOnLeftRegex, content)
                            || (!hasSafeDivGuard && HasMatchOutsideStringLiteral(FloatCastWithEqualityRegex, content))
                            || (!hasSafeDivGuard && HasMatchOutsideStringLiteral(FloatTypeWithEqualityRegex, content));

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

    private static int GetFirstOperatorIndex(string content)
    {
        int min = int.MaxValue;
        UpdateMinOperator(FloatLiteralOnRightRegex,   content, ref min);
        UpdateMinOperator(FloatLiteralOnLeftRegex,    content, ref min);
        UpdateMinOperator(FloatCastWithEqualityRegex, content, ref min);
        UpdateMinOperator(FloatTypeWithEqualityRegex, content, ref min);
        return min == int.MaxValue ? 0 : min;
    }

    private static void UpdateMinOperator(Regex regex, string content, ref int min)
    {
        var m = regex.Match(content);
        if (m.Success)
        {
            int opIdx = GetEqualityOperatorIndex(m);
            if (opIdx < min) min = opIdx;
        }
    }

    /// <summary>
    /// Returns the absolute index of the equality operator (<c>==</c> or <c>!=</c>)
    /// within <paramref name="match"/>. Falls back to <see cref="Match.Index"/> if not found.
    /// </summary>
    private static int GetEqualityOperatorIndex(Match match)
    {
        for (int i = 0; i < match.Value.Length - 1; i++)
        {
            char c = match.Value[i], n = match.Value[i + 1];
            if ((c == '=' && n == '=') || (c == '!' && n == '='))
                return match.Index + i;
        }
        return match.Index;
    }

    /// <summary>
    /// Returns true if <paramref name="position"/> falls inside a string literal in <paramref name="content"/>.
    /// Handles regular strings (with \" escape) and verbatim strings (@"..." with "" escape).
    /// Does not handle raw string literals (""" ... """); when syntax context is unavailable,
    /// raw string literals may produce false positives.
    /// </summary>
    private static bool IsInsideStringLiteralAt(string content, int position)
    {
        bool inString = false;
        bool isVerbatim = false;
        int i = 0;

        while (i < content.Length)
        {
            if (i == position) return inString;

            char c = content[i];

            if (!inString)
            {
                if (c == '@' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    inString = true;
                    isVerbatim = true;
                    i += 2; // skip @"
                    continue;
                }
                if (c == '"')
                {
                    inString = true;
                    isVerbatim = false;
                    i++;
                    continue;
                }
            }
            else if (isVerbatim)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    i += 2; // escaped quote "" in verbatim string
                    continue;
                }
                if (c == '"')
                {
                    inString = false;
                    i++;
                    continue;
                }
            }
            else // regular string
            {
                if (c == '\\')
                {
                    i += 2; // skip escape sequence
                    continue;
                }
                if (c == '"')
                {
                    inString = false;
                    i++;
                    continue;
                }
            }

            i++;
        }

        return inString;
    }

    /// <summary>
    /// Returns true if <paramref name="regex"/> has at least one match in <paramref name="content"/>
    /// that falls outside a string literal.
    /// </summary>
    private static bool HasMatchOutsideStringLiteral(Regex regex, string content)
    {
        foreach (Match match in regex.Matches(content))
        {
            int opIdx = GetEqualityOperatorIndex(match);
            if (!IsInsideStringLiteralAt(content, opIdx))
                return true;
        }
        return false;
    }

}

