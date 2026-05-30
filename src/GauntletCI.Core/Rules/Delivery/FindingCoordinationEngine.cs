// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Delivery;

/// <summary>
/// Ports Phase 21/23 rule coordinations from corpus silver labeling to the analyze path.
/// Boosts confidence on related findings when compound-risk patterns are present.
/// </summary>
public static class FindingCoordinationEngine
{
    /// <summary>
    /// Applies coordination boosts in-place. Returns the number of findings whose confidence increased.
    /// </summary>
    public static int Apply(IList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        if (findings.Count == 0)
            return 0;

        var ruleIds = findings.Select(f => f.RuleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var boosts = 0;

        boosts += ApplyAsyncExecutionCoordination(findings, ruleIds);
        boosts += ApplyExceptionHandlingCoordination(findings, ruleIds);
        boosts += ApplyResourceManagementCoordination(findings, ruleIds);
        boosts += ApplyDataSecurityCoordination(findings, ruleIds);

        return boosts;
    }

    private static int ApplyAsyncExecutionCoordination(IList<Finding> findings, HashSet<string> ruleIds)
    {
        if (!ruleIds.Contains("GCI0016"))
            return 0;

        var boosts = 0;
        boosts += BoostRule(findings, "GCI0039", Confidence.Medium);
        boosts += BoostRule(findings, "GCI0044", Confidence.Medium);
        return boosts;
    }

    private static int ApplyExceptionHandlingCoordination(IList<Finding> findings, HashSet<string> ruleIds)
    {
        var boosts = 0;

        if (ruleIds.Contains("GCI0032") && ruleIds.Contains("GCI0003"))
        {
            boosts += BoostRule(findings, "GCI0003", Confidence.High);
            boosts += BoostRule(findings, "GCI0032", Confidence.Medium);
        }

        if (ruleIds.Contains("GCI0032") && ruleIds.Contains("GCI0016"))
        {
            boosts += BoostRule(findings, "GCI0016", Confidence.High);
            boosts += BoostRule(findings, "GCI0032", Confidence.Medium);
        }

        return boosts;
    }

    private static int ApplyResourceManagementCoordination(IList<Finding> findings, HashSet<string> ruleIds)
    {
        if (!ruleIds.Contains("GCI0024") || !ruleIds.Contains("GCI0015"))
            return 0;

        var boosts = 0;
        boosts += BoostRule(findings, "GCI0024", Confidence.Medium);
        boosts += BoostRule(findings, "GCI0015", Confidence.Medium);
        return boosts;
    }

    private static int ApplyDataSecurityCoordination(IList<Finding> findings, HashSet<string> ruleIds)
    {
        if (!ruleIds.Contains("GCI0015") || !ruleIds.Contains("GCI0029"))
            return 0;

        var boosts = 0;
        boosts += BoostRule(findings, "GCI0015", Confidence.High);
        boosts += BoostRule(findings, "GCI0029", Confidence.High);
        return boosts;
    }

    private static int BoostRule(IList<Finding> findings, string ruleId, Confidence minimum)
    {
        var boosts = 0;
        foreach (var finding in findings.Where(f => f.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase)))
        {
            if (finding.Confidence >= minimum)
                continue;

            finding.Confidence = minimum;
            boosts++;
        }

        return boosts;
    }
}
