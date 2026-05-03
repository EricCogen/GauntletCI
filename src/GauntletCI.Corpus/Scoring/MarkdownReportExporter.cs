// SPDX-License-Identifier: Elastic-2.0
using System.Text;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Scoring;

public sealed class MarkdownReportExporter
{
    private readonly IScoreAggregator _aggregator;

    public MarkdownReportExporter(IScoreAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<string> ExportMarkdownAsync(CancellationToken cancellationToken = default)
    {
        var scorecards = await _aggregator.ScoreAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var gold      = scorecards.Where(s => s.Tier == FixtureTier.Gold).OrderBy(s => s.RuleId).ToList();
        var silver    = scorecards.Where(s => s.Tier == FixtureTier.Silver).OrderBy(s => s.RuleId).ToList();
        var discovery = scorecards.Where(s => s.Tier == FixtureTier.Discovery).OrderBy(s => s.RuleId).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# GauntletCI Corpus Scorecard");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- Gold scorecards: {gold.Count} | Silver scorecards: {silver.Count} | Discovery scorecards: {discovery.Count}");
        sb.AppendLine();

        // Caveat banner when there are no trusted labels
        if (gold.Count == 0 && silver.Count == 0 && discovery.Count > 0)
        {
            sb.AppendLine("> Warning: **No labeled fixtures exist.** All metrics below are operational " +
                          "(trigger rate + Unknown count only). Precision and Recall cannot be computed " +
                          "without ground-truth labels. Add gold fixtures via `corpus ingest` or run " +
                          "`corpus label-all` to generate heuristic silver labels.");
            sb.AppendLine();
        }

        AppendGoldSilverSection(sb, gold, "Gold", trusted: true);
        AppendGoldSilverSection(sb, silver, "Silver _(directional -- heuristic labels)_", trusted: false);
        AppendDiscoverySection(sb, discovery);

        return sb.ToString();
    }

    // -- Section renderers -----------------------------------------------------

    private static void AppendGoldSilverSection(StringBuilder sb, IReadOnlyList<RuleScorecard> scorecards, string heading, bool trusted)
    {
        if (scorecards.Count == 0) return;

        sb.AppendLine($"## {heading} Metrics");
        if (!trusted)
            sb.AppendLine("_Metrics derived from heuristic labels -- treat as directional, not definitive._");
        sb.AppendLine();
        sb.AppendLine("| Rule | Labeled | TP | FP | FN | TN | Unknown | Precision | Recall | Trigger Rate |");
        sb.AppendLine("|------|--------:|---:|---:|---:|---:|--------:|----------:|-------:|-------------:|");

        foreach (var sc in scorecards)
        {
            var precision = (sc.TruePositives + sc.FalsePositives) > 0
                ? $"{sc.Precision * 100:F1}%"
                : "--";
            var recall = (sc.TruePositives + sc.FalseNegatives) > 0
                ? $"{sc.Recall * 100:F1}%"
                : "--";

            sb.AppendLine(
                $"| {sc.RuleId} | {sc.Fixtures} | {sc.TruePositives} | {sc.FalsePositives} | " +
                $"{sc.FalseNegatives} | {sc.TrueNegatives} | {sc.Unknown} | {precision} | {recall} | {sc.TriggerRate * 100:F1}% |");
        }
        sb.AppendLine();
    }

    private static void AppendDiscoverySection(StringBuilder sb, IReadOnlyList<RuleScorecard> scorecards)
    {
        if (scorecards.Count == 0) return;

        sb.AppendLine("## Discovery Operational Metrics");
        sb.AppendLine("_Discovery fixtures are unlabeled -- precision/recall are not reported. " +
                      "Unknown = rule fired but no label exists._");
        sb.AppendLine();
        sb.AppendLine("| Rule | Trigger Rate | Fired (Unknown) | Avg Usefulness |");
        sb.AppendLine("|------|-------------:|----------------:|---------------:|");

        foreach (var sc in scorecards)
        {
            sb.AppendLine(
                $"| {sc.RuleId} | {sc.TriggerRate * 100:F1}% | {sc.Unknown} | {sc.AvgUsefulness:F1}/5 |");
        }
        sb.AppendLine();
    }
}
