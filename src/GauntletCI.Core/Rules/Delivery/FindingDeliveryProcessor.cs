// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Delivery;

/// <summary>
/// Post-processes rule findings for delivery: coordination boosts, file-level demotion,
/// per-rule caps, ranked output, and global limit.
/// </summary>
public static class FindingDeliveryProcessor
{
    /// <summary>Result of delivery processing including the filtered findings and summary metrics.</summary>
    public sealed class Result
    {
        public required IReadOnlyList<Finding> Findings { get; init; }
        public required FindingDeliverySummary Summary { get; init; }
    }

    /// <summary>
    /// Applies delivery policy to <paramref name="findings"/> without mutating the input list.
    /// </summary>
    public static Result Apply(IReadOnlyList<Finding> findings, FindingDeliveryConfig config)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(config);

        var inputCount = findings.Count;
        if (!config.Enabled || inputCount == 0)
        {
            return new Result
            {
                Findings = findings.ToList(),
                Summary = new FindingDeliverySummary
                {
                    InputCount = inputCount,
                    OutputCount = inputCount,
                },
            };
        }

        var working = findings.Select(CloneFinding).ToList();
        var coordinationBoosts = FindingCoordinationEngine.Apply(working);

        var demoted = DemoteFileLevelFindings(working, config.FileLevelRulesToDemote);
        var afterDemotion = working.Count;

        var (capped, droppedByCap) = ApplyPerRulePerFileCaps(working, config);
        var ranked = RankFindings(capped);
        var (final, droppedByGlobal) = ApplyGlobalCap(ranked, config.GlobalMaxFindings);

        return new Result
        {
            Findings = final,
            Summary = new FindingDeliverySummary
            {
                InputCount = inputCount,
                OutputCount = final.Count,
                DroppedByFileLevelDemotion = demoted,
                DroppedByPerRuleCap = droppedByCap,
                DroppedByGlobalCap = droppedByGlobal,
                CoordinationBoostsApplied = coordinationBoosts,
            },
        };
    }

    private static Finding CloneFinding(Finding source) => new()
    {
        RuleId = source.RuleId,
        RuleName = source.RuleName,
        Summary = source.Summary,
        Evidence = source.Evidence,
        WhyItMatters = source.WhyItMatters,
        SuggestedAction = source.SuggestedAction,
        Confidence = source.Confidence,
        Severity = source.Severity,
        SeverityOverride = source.SeverityOverride,
        FilePath = source.FilePath,
        Line = source.Line,
        LlmExplanation = source.LlmExplanation,
        ExpertContext = source.ExpertContext,
        CodeSnippet = source.CodeSnippet,
        CoverageNote = source.CoverageNote,
        TicketContext = source.TicketContext,
    };

    private static int DemoteFileLevelFindings(List<Finding> findings, string[] rulesToDemote)
    {
        if (rulesToDemote.Length == 0)
            return 0;

        var hasLineAnchored = findings.Any(f => f.Line.HasValue);
        if (!hasLineAnchored)
            return 0;

        var demoteSet = rulesToDemote.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var before = findings.Count;
        findings.RemoveAll(f =>
            demoteSet.Contains(f.RuleId) &&
            !f.Line.HasValue);
        return before - findings.Count;
    }

    private static (List<Finding> Kept, int Dropped) ApplyPerRulePerFileCaps(
        List<Finding> findings,
        FindingDeliveryConfig config)
    {
        var groups = findings
            .GroupBy(f => $"{f.RuleId}|{f.FilePath ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .ToList();

        var kept = new List<Finding>(findings.Count);
        var dropped = 0;

        foreach (var group in groups)
        {
            var ruleId = group.First().RuleId;
            var cap = config.PerRulePerFileCap.TryGetValue(ruleId, out var ruleCap)
                ? ruleCap
                : config.DefaultPerRulePerFileCap;

            var ordered = group
                .OrderByDescending(ComputeDeliveryScore)
                .ThenBy(f => f.Line ?? int.MaxValue)
                .ToList();

            kept.AddRange(ordered.Take(cap));
            dropped += Math.Max(0, ordered.Count - cap);
        }

        return (kept, dropped);
    }

    private static List<Finding> RankFindings(IEnumerable<Finding> findings) =>
        findings
            .OrderByDescending(ComputeDeliveryScore)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line ?? int.MaxValue)
            .ToList();

    private static (List<Finding> Kept, int Dropped) ApplyGlobalCap(List<Finding> ranked, int globalMax)
    {
        if (globalMax <= 0 || ranked.Count <= globalMax)
            return (ranked, 0);

        return (ranked.Take(globalMax).ToList(), ranked.Count - globalMax);
    }

    /// <summary>
    /// Higher scores surface first: Block severity, line anchoring, and confidence dominate.
    /// </summary>
    internal static int ComputeDeliveryScore(Finding finding) =>
        SeverityScore(finding.Severity)
        + (finding.Line.HasValue ? 50 : 0)
        + (string.IsNullOrEmpty(finding.FilePath) ? 0 : 10)
        + ConfidenceScore(finding.Confidence);

    private static int SeverityScore(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Block => 1000,
        RuleSeverity.Warn => 100,
        RuleSeverity.Advisory => 50,
        RuleSeverity.Info => 10,
        _ => 0,
    };

    private static int ConfidenceScore(Confidence confidence) => confidence switch
    {
        Confidence.High => 30,
        Confidence.Medium => 20,
        Confidence.Low => 0,
        _ => 0,
    };
}
