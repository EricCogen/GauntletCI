// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using System.Text.RegularExpressions;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Floating-point literal and cast patterns used to detect unsafe equality comparisons.
/// </summary>
internal static class FloatingPointPatterns
{
    /// <summary>Regex: matches == or != followed by a float/double literal on the right side.</summary>
    public static readonly Regex FloatLiteralOnRightRegex = new(
        @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\b",
        RegexOptions.Compiled);

    /// <summary>Regex: matches float/double literal on the left side of == or !=.</summary>
    public static readonly Regex FloatLiteralOnLeftRegex = new(
        @"\b(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\s*(?:==|!=)",
        RegexOptions.Compiled);

    /// <summary>Regex: matches a (float) or (double) cast alongside == or !=.</summary>
    public static readonly Regex FloatCastWithEqualityRegex = new(
        @"\((?:float|double)\).*(?:==|!=)|(?:==|!=).*\((?:float|double)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Regex: matches a float or double type keyword alongside == or !=.</summary>
    public static readonly Regex FloatTypeWithEqualityRegex = new(
        @"\b(?:float|double)\b.*(?:==|!=)|(?:==|!=).*\b(?:float|double)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Regex: matches the safe-division guard pattern (integer zero-check with ternary).</summary>
    public static readonly Regex IntegerZeroGuardRegex = new(
        @"(?:==|!=)\s*0\s*\?", RegexOptions.Compiled);

    /// <summary>Returns <c>true</c> if the given content is a guarded integer zero check (safe division pattern).</summary>
    public static bool IsGuardedIntegerZeroCheck(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return IntegerZeroGuardRegex.IsMatch(content);
    }
}
