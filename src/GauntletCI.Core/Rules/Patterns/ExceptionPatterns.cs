// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect uncaught exception paths and exception handling issues.
/// </summary>
#pragma warning disable GCI0032  // Uncaught Exception Path - pattern data only
internal static class ExceptionPatterns
{
    /// <summary>
    /// Test assertion methods that validate exception handling (Assert.Throws, Should().Throw, etc.).
    /// Used by GCI0032 to determine whether throw new statements are covered by tests.
    /// These are PATTERN STRINGS for exception detection, not actual exception throws - no GCI0032 violation.
    /// </summary>
    public static readonly string[] ThrowAssertions =
    [
        "Assert.Throws", ".Should().Throw", "ThrowsAsync", "ThrowsExceptionAsync", "Throws<"
    ];

    /// <summary>
    /// Guard clause throws (ArgumentNullException, etc.) that are defensive programming patterns
    /// and do not require test coverage in the same diff (they protect preconditions, not logic paths).
    /// Used by GCI0032 to exclude guard clause throws from uncaught exception detection.
    /// These are PATTERN STRINGS for exception pattern matching, not actual throws - no GCI0032 violation.
    /// </summary>
    public static readonly string[] GuardClauseThrows =
    [
        "throw new ArgumentNullException",
        "throw new ArgumentException",
        "throw new ArgumentOutOfRangeException",
        "throw new ObjectDisposedException",
        "throw new InvalidOperationException",
        "throw new NotSupportedException",
        "throw new FormatException",
        "throw new IndexOutOfRangeException",
        "throw new KeyNotFoundException",
        "throw new UnauthorizedAccessException",
    ];
}
#pragma warning restore GCI0032
