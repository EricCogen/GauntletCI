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
/// Patterns used to detect architectural boundary violations and policy violations.
/// </summary>
internal static class ArchitecturePatterns
{
    /// <summary>
    /// Regex: matches C# using directives to extract the imported namespace.
    /// Used by GCI0035 to validate imports against configured forbidden import pairs.
    /// </summary>
    public static readonly Regex UsingRegex =
        new(@"^\s*using\s+([\w.]+)\s*;", RegexOptions.Compiled);
}
