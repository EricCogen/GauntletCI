// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Scoring;

/// <summary>
/// Compares actual rule findings against expected labels and produces an
/// explicit <see cref="FindingEvaluation"/> for every relevant (rule, fixture) pair.
///
/// Classification rules:
///   fired + label.ShouldTrigger=true  → TruePositive
///   fired + label.ShouldTrigger=false → FalsePositive
///   !fired + label.ShouldTrigger=true → FalseNegative
///   !fired + label.ShouldTrigger=false → TrueNegative
///   fired + no label                  → Unknown
///   !fired + no label                 → skipped (too numerous, no signal)
/// </summary>
public sealed class EvaluationClassifier : IEvaluationClassifier
{
    public IReadOnlyList<FindingEvaluation> Classify(
        FixtureMetadata fixture,
        IReadOnlyList<ExpectedFinding> expectedFindings,
        IReadOnlyList<ActualFinding> actualFindings)
    {
        var results = new List<FindingEvaluation>();

        var firedRules = actualFindings
            .Where(a => a.DidTrigger)
            .Select(a => a.RuleId)
            .ToHashSet(StringComparer.Ordinal);

        // Exclude inconclusive labels
        var labelByRule = expectedFindings
            .Where(e => !e.IsInconclusive)
            .ToDictionary(e => e.RuleId, e => e, StringComparer.Ordinal);

        // Evaluate every rule that fired
        foreach (var ruleId in firedRules)
        {
            if (labelByRule.TryGetValue(ruleId, out var label))
            {
                results.Add(new FindingEvaluation
                {
                    FixtureId       = fixture.FixtureId,
                    RuleId          = ruleId,
                    Tier            = fixture.Tier,
                    Status          = label.ShouldTrigger
                                        ? EvaluationStatus.TruePositive
                                        : EvaluationStatus.FalsePositive,
                    LabelConfidence = label.LabelSource == LabelSource.HumanReview
                                        ? LabelConfidence.Trusted
                                        : LabelConfidence.Heuristic,
                    LabelReason     = label.Reason,
                });
            }
            else
            {
                // Fired but no label — must be visible, not silently dropped
                results.Add(new FindingEvaluation
                {
                    FixtureId       = fixture.FixtureId,
                    RuleId          = ruleId,
                    Tier            = fixture.Tier,
                    Status          = EvaluationStatus.Unknown,
                    LabelConfidence = LabelConfidence.Unknown,
                });
            }
        }

        // Evaluate every label where the rule did NOT fire
        foreach (var (ruleId, label) in labelByRule)
        {
            if (firedRules.Contains(ruleId)) continue; // already classified above

            results.Add(new FindingEvaluation
            {
                FixtureId       = fixture.FixtureId,
                RuleId          = ruleId,
                Tier            = fixture.Tier,
                Status          = label.ShouldTrigger
                                    ? EvaluationStatus.FalseNegative
                                    : EvaluationStatus.TrueNegative,
                LabelConfidence = label.LabelSource == LabelSource.HumanReview
                                    ? LabelConfidence.Trusted
                                    : LabelConfidence.Heuristic,
                LabelReason     = label.Reason,
            });
        }

        return results;
    }
}
