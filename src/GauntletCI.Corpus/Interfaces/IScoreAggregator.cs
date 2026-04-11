// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

public sealed record RuleScorecard(
    string RuleId,
    FixtureTier Tier,
    int Fixtures,
    double TriggerRate,
    double Precision,
    double Recall,
    double InconclusiveRate,
    double AvgUsefulness,
    string Notes);

public interface IScoreAggregator
{
    Task<IReadOnlyList<RuleScorecard>> ScoreAsync(
        string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default);
}
