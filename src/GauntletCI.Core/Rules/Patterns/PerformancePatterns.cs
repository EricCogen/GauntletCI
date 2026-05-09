// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect performance hotpath issues (LINQ in loops, Thread.Sleep, etc.).
/// </summary>
internal static class PerformancePatterns
{
    /// <summary>LINQ method calls that should not be used inside loops.</summary>
    public static readonly string[] LinqMethods =
    [
        ".Where(", ".Select(", ".FirstOrDefault(", ".Any(", ".Count("
    ];

    /// <summary>Loop keywords that should not contain blocking operations or unbounded operations.</summary>
    public static readonly string[] LoopKeywords =
    [
        "for (", "foreach (", "while ("
    ];

    /// <summary>Loop keywords where unbounded collection growth is a concern (for/while, not foreach).</summary>
    public static readonly string[] UnboundedLoopKeywords =
    [
        "for (", "while ("
    ];

    /// <summary>Returns <c>true</c> if the given content contains a LINQ method call.</summary>
    public static bool HasLinqCall(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        // GCI0044: This is a pattern detection helper, not a performance-sensitive query
        return LinqMethods.Any(m => content.Contains(m, StringComparison.Ordinal));
    }

    /// <summary>Returns <c>true</c> if the given content contains a loop construct.</summary>
    public static bool HasLoopConstruct(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return LoopKeywords.Any(k => content.Contains(k, StringComparison.Ordinal));
    }

    /// <summary>Returns <c>true</c> if the given path is a rule implementation file (hotpath guard).</summary>
    public static bool IsRuleImplementationFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.Contains("Rules/Implementations", StringComparison.OrdinalIgnoreCase) ||
               path.Contains(@"Rules\Implementations", StringComparison.OrdinalIgnoreCase);
    }
}
