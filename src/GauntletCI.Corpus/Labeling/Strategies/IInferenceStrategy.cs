// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Strategy for applying a single inference heuristic (or related group of heuristics)
/// to a fixture's diff. Each strategy encapsulates pattern-matching logic for one or more
/// rules and emits labels based on what it finds.
/// </summary>
public interface IInferenceStrategy
{
    /// <summary>
    /// Applies this strategy's heuristics to the given diff context.
    /// </summary>
    /// <param name="fixtureId">The corpus fixture ID (for logging/tracing).</param>
    /// <param name="context">Pre-parsed diff components and raw diff text.</param>
    /// <returns>List of inferred labels (empty if no heuristics matched).</returns>
    IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context);
}
