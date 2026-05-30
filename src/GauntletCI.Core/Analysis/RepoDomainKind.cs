// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Analysis;

/// <summary>High-level repository domain used to gate web/DI-specific rules (PG-DOMAIN).</summary>
public enum RepoDomainKind
{
    /// <summary>Could not classify; domain rules are not suppressed.</summary>
    Unknown = 0,

    /// <summary>Class library, client SDK, or infrastructure package without ASP.NET host.</summary>
    ClassLibrary = 1,

    /// <summary>Web app, API host, or ASP.NET composition root.</summary>
    WebApplication = 2,
}
