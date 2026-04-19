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
            // Block — commit-blocking by default
            ["GCI0001"] = RuleSeverity.Block,
            ["GCI0003"] = RuleSeverity.Block,
            ["GCI0004"] = RuleSeverity.Block,
            ["GCI0007"] = RuleSeverity.Block,
            ["GCI0010"] = RuleSeverity.Block,
            ["GCI0012"] = RuleSeverity.Block,
            ["GCI0015"] = RuleSeverity.Block,
            ["GCI0016"] = RuleSeverity.Block,
            ["GCI0021"] = RuleSeverity.Block,
            ["GCI0032"] = RuleSeverity.Block,
            ["GCI0036"] = RuleSeverity.Block,
            ["GCI0039"] = RuleSeverity.Block,

            ["GCI0052"] = RuleSeverity.Block,

            // Warn — visible by default, non-blocking
            ["GCI0053"] = RuleSeverity.Warn,
            ["GCI0006"] = RuleSeverity.Warn,
            ["GCI0022"] = RuleSeverity.Warn,
            ["GCI0024"] = RuleSeverity.Warn,
            ["GCI0029"] = RuleSeverity.Warn,
            ["GCI0035"] = RuleSeverity.Warn,
            ["GCI0038"] = RuleSeverity.Warn,
            ["GCI0041"] = RuleSeverity.Warn,
        };

    /// <summary>Returns the built-in default severity for <paramref name="ruleId"/>, or <see cref="RuleSeverity.Info"/> if not listed.</summary>
    public static RuleSeverity Get(string ruleId) =>
        Map.TryGetValue(ruleId, out var s) ? s : RuleSeverity.Info;
}
