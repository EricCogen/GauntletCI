// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Delivery;

/// <summary>Suppresses web/DI-specific rule findings on class-library repositories (PG-DOMAIN).</summary>
public static class DomainFindingProcessor
{
    /// <summary>Result of domain filtering.</summary>
    public sealed class Result
    {
        public required IReadOnlyList<Finding> Findings { get; init; }
        public int DroppedCount { get; init; }
    }

    /// <summary>
    /// Removes findings for rules that assume a web/DI host when the repo is classified as a library.
    /// </summary>
    public static Result Apply(
        IReadOnlyList<Finding> findings,
        RepoDomainProfile profile,
        RepoDomainConfig config)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Enabled || !profile.IsClassLibrary || findings.Count == 0)
        {
            return new Result { Findings = findings.ToList(), DroppedCount = 0 };
        }

        var suppress = config.LibrarySuppressRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = new List<Finding>(findings.Count);
        var dropped = 0;

        foreach (var finding in findings)
        {
            if (suppress.Contains(finding.RuleId))
                dropped++;
            else
                kept.Add(finding);
        }

        return new Result { Findings = kept, DroppedCount = dropped };
    }
}
