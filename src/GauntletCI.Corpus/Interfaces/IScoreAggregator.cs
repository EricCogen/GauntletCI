// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

/// <summary>
/// Per-rule scorecard with explicit TP/FP/FN/TN/Unknown classification counts.
/// Precision and Recall are computed from labeled fixtures only; Unknown is reported
/// separately so unlabeled-but-fired cases are never silently dropped.
/// </summary>
public sealed record RuleScorecard(
    string RuleId,
    FixtureTier Tier,
    int Fixtures,
    double TriggerRate,
    double Precision,
    double Recall,
    double InconclusiveRate,
    double AvgUsefulness,
    string Notes,
    int TruePositives = 0,
    int FalsePositives = 0,
    int FalseNegatives = 0,
    int TrueNegatives = 0,
    int Unknown = 0
);

public interface IScoreAggregator
{
    Task<IReadOnlyList<RuleScorecard>> ScoreAsync(
        string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default);
}
