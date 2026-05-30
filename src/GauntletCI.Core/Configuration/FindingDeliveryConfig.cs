// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Controls post-rule finding delivery: per-rule caps, global limits, ranking, and file-level demotion.
/// Part of platform gap PG-DELIVERY.
/// </summary>
public class FindingDeliveryConfig
{
    /// <summary>When false, findings pass through unchanged (coordination and caps skipped).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum findings returned after ranking. Additional findings are dropped.</summary>
    public int GlobalMaxFindings { get; set; } = 25;

    /// <summary>Default cap per (rule, file) group before global ranking.</summary>
    public int DefaultPerRulePerFileCap { get; set; } = 5;

    /// <summary>Per-rule overrides for noisy rules (key: rule ID, value: max findings per file).</summary>
    public Dictionary<string, int> PerRulePerFileCap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GCI0038"] = 3,
        ["GCI0043"] = 3,
        ["GCI0006"] = 5,
        ["GCI0044"] = 3,
    };

    /// <summary>
    /// File-level-only findings from these rules are dropped when any line-anchored finding exists.
    /// </summary>
    public string[] FileLevelRulesToDemote { get; set; } = ["GCI0001", "GCI0003"];
}
