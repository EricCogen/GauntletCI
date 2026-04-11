// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class FixtureEvaluation
{
    public string FixtureId { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string PrecisionBucket { get; init; } = string.Empty;
    public string RecallBucket { get; init; } = string.Empty;
    public double Usefulness { get; init; }
    public string ReviewerNotes { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
}
