// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Delivery;

/// <summary>Suppresses findings anchored to added lines that are relocated, not net-new (PG-PROVENANCE / RC-1).</summary>
public static class ProvenanceFindingProcessor
{
    /// <summary>Result of provenance filtering.</summary>
    public sealed class Result
    {
        public required IReadOnlyList<Finding> Findings { get; init; }
        public int DroppedCount { get; init; }
    }

    /// <summary>
    /// Removes line-anchored findings on relocated added lines unless the rule is exempt.
    /// </summary>
    public static Result Apply(
        IReadOnlyList<Finding> findings,
        DiffProvenanceAnalyzer.Index provenance,
        ProvenanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Enabled || findings.Count == 0)
        {
            return new Result { Findings = findings.ToList(), DroppedCount = 0 };
        }

        var exempt = config.ExemptRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = new List<Finding>(findings.Count);
        var dropped = 0;

        foreach (var finding in findings)
        {
            if (ShouldKeep(finding, provenance, exempt))
                kept.Add(finding);
            else
                dropped++;
        }

        return new Result { Findings = kept, DroppedCount = dropped };
    }

    private static bool ShouldKeep(
        Finding finding,
        DiffProvenanceAnalyzer.Index provenance,
        HashSet<string> exemptRules)
    {
        if (exemptRules.Contains(finding.RuleId))
            return true;

        if (!finding.Line.HasValue)
            return true;

        return !provenance.IsRelocated(finding.FilePath, finding.Line.Value);
    }
}
