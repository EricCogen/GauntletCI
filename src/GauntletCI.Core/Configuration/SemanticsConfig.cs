// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Semantic patch analysis settings (PG-SEMANTICS). Enriches findings with counterfactual witnesses.
/// </summary>
public class SemanticsConfig
{
    /// <summary>When false, semantic enrichment is skipped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rules whose confidence is boosted when a conditional-modification counterfactual aligns with the finding line.
    /// </summary>
    public string[] BoostRules { get; set; } =
    [
        "GCI0058",
        "GCI0059",
        "GCI0003",
        "GCI0007",
    ];
}
