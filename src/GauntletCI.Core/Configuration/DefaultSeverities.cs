// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Configuration;

/// <summary>
/// Built-in default severity mapping for all GauntletCI rules.
/// Rules not listed here default to <see cref="RuleSeverity.Info"/>.
/// All defaults are overridable via <c>.gauntletci.json</c> or <c>.editorconfig</c>.
/// </summary>
internal static class DefaultSeverities
{
    private static readonly Dictionary<string, RuleSeverity> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Block: commit-blocking by default (rule-level; some rules use SeverityOverride per finding)
            ["GCI0003"] = RuleSeverity.Block,
            ["GCI0007"] = RuleSeverity.Block,
            ["GCI0010"] = RuleSeverity.Block,
            ["GCI0012"] = RuleSeverity.Block,
            ["GCI0015"] = RuleSeverity.Block,
            ["GCI0016"] = RuleSeverity.Block,
            ["GCI0020"] = RuleSeverity.Block,
            ["GCI0021"] = RuleSeverity.Block,
            ["GCI0036"] = RuleSeverity.Block,
            ["GCI0039"] = RuleSeverity.Block,
            ["GCI0052"] = RuleSeverity.Block,

            // Warn: visible by default, non-blocking
            ["GCI0001"] = RuleSeverity.Info,
            ["GCI0004"] = RuleSeverity.Warn,
            ["GCI0006"] = RuleSeverity.Warn,
            ["GCI0022"] = RuleSeverity.Warn,
            ["GCI0024"] = RuleSeverity.Warn,
            ["GCI0029"] = RuleSeverity.Warn,
            ["GCI0032"] = RuleSeverity.Warn,
            ["GCI0035"] = RuleSeverity.Warn,
            ["GCI0038"] = RuleSeverity.Warn,
            ["GCI0041"] = RuleSeverity.Warn,
            ["GCI0048"] = RuleSeverity.Warn,
            ["GCI0050"] = RuleSeverity.Warn,
            ["GCI0051"] = RuleSeverity.Warn,
            ["GCI0053"] = RuleSeverity.Warn,
            ["GCI0057"] = RuleSeverity.Warn,

            // Info: advisory / low-noise heuristics
            ["GCI0042"] = RuleSeverity.Info,
            ["GCI0043"] = RuleSeverity.Info,
            ["GCI0044"] = RuleSeverity.Info,
            ["GCI0045"] = RuleSeverity.Info,
            ["GCI0046"] = RuleSeverity.Info,
            ["GCI0047"] = RuleSeverity.Info,
            ["GCI0049"] = RuleSeverity.Info,
            ["GCI0056"] = RuleSeverity.Info,
            ["GCI0058"] = RuleSeverity.Block,

            // None: disabled by default (duplicate coverage elsewhere)
            ["GCI0054"] = RuleSeverity.None, // duplicate coverage — see GCI0016
            ["GCI0055"] = RuleSeverity.None, // signature changes — covered by GCI0003
        };

    /// <summary>Returns the built-in default severity for <paramref name="ruleId"/>, or <see cref="RuleSeverity.Info"/> if not listed.</summary>
    public static RuleSeverity Get(string ruleId) =>
        Map.TryGetValue(ruleId, out var s) ? s : RuleSeverity.Info;
}
