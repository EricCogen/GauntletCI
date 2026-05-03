// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect data integrity risks, unsafe input handling, and conflicting data operations.
/// </summary>
internal static class DataIntegrityPatterns
{
    /// <summary>
    /// HTTP context signals indicating user input boundaries.
    /// Used by GCI0015 for detecting mass-assignment and unsafe casting in HTTP request context.
    /// </summary>
    public static readonly string[] HttpContextSignals =
    [
        "Request.Form", "Request.Query", "Request.Body",
        "HttpContext.Request", "[FromBody]", "[FromForm]", "[FromQuery]"
    ];

    /// <summary>
    /// SQL patterns that silently ignore or suppress insert/update conflicts.
    /// Used by GCI0015 to detect situations where data integrity violations are hidden.
    /// These are PATTERN STRINGS, not actual SQL commands - no GCI0015 violation.
    /// GCI0015 false positive suppression: this is pattern data for a detection rule.
    /// </summary>
    #pragma warning disable GCI0015  // Data Integrity Risk - pattern data only
    public static readonly string[] SqlIgnorePatterns =
    [
        "INSERT IGNORE", "ON CONFLICT DO NOTHING", "INSERT OR IGNORE"
    ];
    #pragma warning restore GCI0015

    /// <summary>
    /// Numeric cast patterns that can cause silent data truncation or overflow.
    /// Used by GCI0015 for detecting unchecked casts on potentially user-supplied values.
    /// These are PATTERN STRINGS, not actual casts - no GCI0015 violation.
    /// GCI0015 false positive suppression: this is pattern data for a detection rule.
    /// </summary>
    #pragma warning disable GCI0015  // Data Integrity Risk - pattern data only
    public static readonly string[] UncheckedCastPatterns =
    [
        "(int)", "(long)", "(decimal)", "(float)", "(short)"
    ];
    #pragma warning restore GCI0015

    /// <summary>
    /// Returns true if the given content contains an HTTP context signal indicating user input.
    /// </summary>
    public static bool HasHttpContextSignal(string content)
    {
        return HttpContextSignals.Any(signal => content.Contains(signal, StringComparison.Ordinal));
    }
}
