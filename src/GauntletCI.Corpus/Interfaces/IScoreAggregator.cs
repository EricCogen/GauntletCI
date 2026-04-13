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
    int Fixtures,           // total labeled fixture/rule pairs evaluated
    double TriggerRate,     // fired / all fixtures in this tier
    double Precision,       // TP / (TP + FP)  — labeled only
    double Recall,          // TP / (TP + FN)  — labeled only
    double InconclusiveRate,
    double AvgUsefulness,
    string Notes,
    // classification breakdown
    int TruePositives  = 0,
    int FalsePositives = 0,
    int FalseNegatives = 0,
    int TrueNegatives  = 0,
    int Unknown        = 0  // fired but no label
);

public interface IScoreAggregator
{
    Task<IReadOnlyList<RuleScorecard>> ScoreAsync(
        string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default);
}
