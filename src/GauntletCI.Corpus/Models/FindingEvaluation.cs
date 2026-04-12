// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

/// <summary>
/// Classification result for a single (fixture × rule) evaluation.
/// Produced by <c>EvaluationClassifier</c> before aggregation.
/// </summary>
public sealed class FindingEvaluation
{
    public required string FixtureId { get; init; }
    public required string RuleId { get; init; }
    public FixtureTier Tier { get; init; }
    public EvaluationStatus Status { get; init; }
    public LabelConfidence LabelConfidence { get; init; }
    public string? LabelReason { get; init; }
}
