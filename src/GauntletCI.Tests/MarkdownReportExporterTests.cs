// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Scoring;

namespace GauntletCI.Tests;

public class MarkdownReportExporterTests
{
    // -------------------------------------------------------------------------
    // Hand-rolled test double
    // -------------------------------------------------------------------------

    private sealed class FakeAggregator : IScoreAggregator
    {
        private readonly IReadOnlyList<RuleScorecard> _cards;
        public FakeAggregator(params RuleScorecard[] cards) { _cards = cards; }
        public Task<IReadOnlyList<RuleScorecard>> ScoreAsync(string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_cards);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RuleScorecard MakeCard(
        string ruleId,
        FixtureTier tier,
        int tp = 0, int fp = 0, int fn = 0, int tn = 0, int unknown = 0,
        double triggerRate = 0.5, double precision = 0.0, double recall = 0.0,
        double avgUsefulness = 3.0)
        => new(
            RuleId: ruleId,
            Tier: tier,
            Fixtures: tp + fp + fn + tn + unknown,
            TriggerRate: triggerRate,
            Precision: precision,
            Recall: recall,
            InconclusiveRate: 0.0,
            AvgUsefulness: avgUsefulness,
            Notes: "",
            TruePositives: tp,
            FalsePositives: fp,
            FalseNegatives: fn,
            TrueNegatives: tn,
            Unknown: unknown);

    private static MarkdownReportExporter MakeExporter(params RuleScorecard[] cards)
        => new(new FakeAggregator(cards));

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportMarkdownAsync_NoScorecards_ReturnsHeaderOnly()
    {
        var exporter = MakeExporter();

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("# GauntletCI Corpus Scorecard", result);
        Assert.Contains("Generated:", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_WithGoldScorecards_ContainsGoldSection()
    {
        var card = MakeCard("GCI0001", FixtureTier.Gold, tp: 3, fp: 1, fn: 1, tn: 2,
                            triggerRate: 0.5, precision: 0.75, recall: 0.75);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("## Gold Metrics", result);
        Assert.Contains("| Rule | Labeled | TP | FP | FN | TN | Unknown | Precision | Recall | Trigger Rate |", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_WithSilverScorecards_ContainsSilverSection()
    {
        var card = MakeCard("GCI0002", FixtureTier.Silver, tp: 2, fp: 0, fn: 1, tn: 3,
                            triggerRate: 0.4, precision: 1.0, recall: 0.667);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("## Silver", result);
        Assert.Contains("heuristic labels", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_WithDiscoveryOnly_ShowsWarningBanner()
    {
        var card = MakeCard("GCI0003", FixtureTier.Discovery, unknown: 5, triggerRate: 0.6);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("No labeled fixtures exist", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_WithDiscoveryScorecards_ContainsDiscoverySection()
    {
        var card = MakeCard("GCI0003", FixtureTier.Discovery, unknown: 5, triggerRate: 0.6);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("## Discovery Operational Metrics", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_GoldScorecard_PrecisionFormattedAsPercent()
    {
        // TP=3, FP=1 → precision = 3/(3+1) = 0.75 → formatted as 75.0%
        var card = MakeCard("GCI0005", FixtureTier.Gold, tp: 3, fp: 1, fn: 1, tn: 2,
                            triggerRate: 0.5, precision: 0.75, recall: 0.75);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("75.0%", result);
    }

    [Fact]
    public async Task ExportMarkdownAsync_ZeroLabeledFixtures_PrecisionIsDoubleDash()
    {
        // TP=0, FP=0 → precision denominator is zero → "--"
        var card = MakeCard("GCI0006", FixtureTier.Gold, tp: 0, fp: 0, fn: 0, tn: 0,
                            triggerRate: 0.0, precision: 0.0, recall: 0.0);
        var exporter = MakeExporter(card);

        var result = await exporter.ExportMarkdownAsync();

        Assert.Contains("--", result);
    }
}
