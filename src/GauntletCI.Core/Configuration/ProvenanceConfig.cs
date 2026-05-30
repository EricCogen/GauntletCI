// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Diff provenance settings (PG-PROVENANCE). Suppresses findings on added lines that match removed lines (RC-1).
/// </summary>
public class ProvenanceConfig
{
    /// <summary>When false, provenance filtering is skipped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rules that still fire when the anchored line is classified as relocated code.
    /// Signature and cross-entity rules remain enabled by default.
    /// </summary>
    public string[] ExemptRules { get; set; } =
    [
        "GCI0003",
        "GCI0058",
        "GCI0059",
    ];
}
