// SPDX-License-Identifier: Elastic-2.0
using System.Text;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Scoring;

public sealed class MarkdownReportExporter : IReportExporter
{
    private readonly IScoreAggregator _aggregator;

    public MarkdownReportExporter(IScoreAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<string> ExportMarkdownAsync(CancellationToken cancellationToken = default)
    {
        var scorecards = await _aggregator.ScoreAsync(cancellationToken: cancellationToken);

        int gold      = scorecards.Count(s => s.Tier == FixtureTier.Gold);
        int silver    = scorecards.Count(s => s.Tier == FixtureTier.Silver);
        int discovery = scorecards.Count(s => s.Tier == FixtureTier.Discovery);

        var sb = new StringBuilder();
        sb.AppendLine("# GauntletCI Corpus Scorecard");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- Total rules scored: {scorecards.Count}");
        sb.AppendLine($"- Gold scorecards: {gold} | Silver scorecards: {silver} | Discovery scorecards: {discovery}");
        sb.AppendLine();
        sb.AppendLine("## Rule Scorecards");

        foreach (var sc in scorecards.OrderBy(s => s.RuleId).ThenBy(s => s.Tier))
        {
            sb.AppendLine();
            sb.AppendLine($"### {sc.RuleId} — {sc.Tier}");
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Fixtures | {sc.Fixtures} |");
            sb.AppendLine($"| Trigger Rate | {sc.TriggerRate:P1} |");
            sb.AppendLine($"| Precision | {sc.Precision:P1} |");
            sb.AppendLine($"| Recall | {sc.Recall:P1} |");
            sb.AppendLine($"| Inconclusive Rate | {sc.InconclusiveRate:P1} |");
            sb.AppendLine($"| Avg Usefulness | {sc.AvgUsefulness:F1}/5 |");

            if (!string.IsNullOrWhiteSpace(sc.Notes))
                sb.AppendLine($"{Environment.NewLine}> Notes: {sc.Notes}");
        }

        return sb.ToString();
    }
}
