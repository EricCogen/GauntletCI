// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Repo/domain classifier settings (PG-DOMAIN). Suppresses web/DI-specific rules on class libraries.
/// </summary>
public class RepoDomainConfig
{
    /// <summary>When false, domain gating is skipped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Profile override: <c>auto</c> (default), <c>library</c>, or <c>web</c>.
    /// </summary>
    public string Profile { get; set; } = "auto";

    /// <summary>
    /// Rules dropped entirely on <see cref="Analysis.RepoDomainKind.ClassLibrary"/> repos.
    /// </summary>
    public string[] LibrarySuppressRules { get; set; } =
    [
        "GCI0022",
        "GCI0038",
        "GCI0046",
        "GCI0024",
        "GCI0035",
    ];
}
