// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect TODO/stub markers and incomplete code.
/// </summary>
internal static class StubDetectionPatterns
{
    /// <summary>
    /// Stub marker keywords (TODO, FIXME, HACK) that indicate incomplete code requiring resolution before production.
    /// Used by GCI0042 to detect stub comments and incomplete implementations.
    /// </summary>
    public static readonly string[] StubKeywords = ["TODO", "FIXME", "HACK"];
}
