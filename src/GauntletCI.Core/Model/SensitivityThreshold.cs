// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

/// <summary>
/// Named filtering threshold that controls which findings are shown based on the
/// 2D priority grid of Severity x Confidence.
/// </summary>
public enum SensitivityThreshold
{
    /// <summary>Only Block findings with Medium or High confidence. Use when you want minimal noise.</summary>
    Strict,
    /// <summary>All Block findings plus Warn findings with Medium or High confidence. The default.</summary>
    Balanced,
    /// <summary>All Block and Warn findings regardless of confidence. Equivalent to legacy behavior.</summary>
    Permissive,
}

/// <summary>
/// Determines whether a finding passes the configured sensitivity threshold.
/// Severity is the outer gate; Confidence is the inner multiplier.
/// </summary>
public static class SensitivityFilter
{
    /// <summary>
    /// Returns true when a finding with the given severity and confidence should be shown
    /// at the given sensitivity level.
    /// <list type="bullet">
    ///   <item><description>Advisory: always passes (shown regardless of threshold).</description></item>
    ///   <item><description>Info: permissive only (aligned with docs — use <c>--verbose</c> for explicit Info in other modes).</description></item>
    ///   <item><description>None: never passes.</description></item>
    ///   <item><description>strict: Block + Medium or higher only.</description></item>
    ///   <item><description>balanced: all Block; Warn + Medium or higher.</description></item>
    ///   <item><description>permissive: all Block and all Warn.</description></item>
    /// </list>
    /// </summary>
    public static bool Passes(RuleSeverity severity, Confidence confidence, SensitivityThreshold threshold)
    {
        if (severity == RuleSeverity.None)
            return false;
        if (severity == RuleSeverity.Advisory)
            return true;
        if (severity == RuleSeverity.Info)
            return threshold == SensitivityThreshold.Permissive;

        return threshold switch
        {
            SensitivityThreshold.Strict =>
                severity == RuleSeverity.Block && confidence >= Confidence.Medium,
            SensitivityThreshold.Balanced =>
                severity == RuleSeverity.Block ||
                (severity == RuleSeverity.Warn && confidence >= Confidence.Medium),
            SensitivityThreshold.Permissive =>
                severity >= RuleSeverity.Warn,
            _ => severity >= RuleSeverity.Warn,
        };
    }
}
