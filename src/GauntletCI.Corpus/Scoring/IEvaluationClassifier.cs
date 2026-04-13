// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Scoring;

/// <summary>
/// Classifies a fixture's actual findings against its expected labels,
/// producing an explicit <see cref="FindingEvaluation"/> for every
/// (rule, fixture) pair that fired or was labeled.
/// </summary>
public interface IEvaluationClassifier
{
    IReadOnlyList<FindingEvaluation> Classify(
        FixtureMetadata fixture,
        IReadOnlyList<ExpectedFinding> expectedFindings,
        IReadOnlyList<ActualFinding> actualFindings);
}
