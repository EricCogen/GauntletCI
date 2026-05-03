// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Analysis.Enrichment;

/// <summary>
/// Enriches a finding with additional context, metadata, or explanation.
/// Each enricher focuses on a single enrichment concern (LLM explanation, expert knowledge, coverage data, etc.).
/// </summary>
public interface IFindingEnricher
{
    /// <summary>
    /// The name of this enricher stage (e.g., "LlmExplanation", "ExpertKnowledge", "CoverageData").
    /// Used for diagnostics and pipeline ordering.
    /// </summary>
    string StageName { get; }

    /// <summary>
    /// Whether this enricher should run.
    /// May be disabled based on configuration, availability of external services, or dependencies.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Enriches a single finding with context from this enricher's domain.
    /// Must be a pure function: no side effects, no state mutations outside the finding.
    /// </summary>
    /// <param name="finding">The finding to enrich. May be modified in-place.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if enrichment succeeded, false if skipped (e.g., already enriched or not applicable).</returns>
    Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default);

    /// <summary>
    /// Returns the stage names of enrichers this stage depends on.
    /// The pipeline will execute dependencies before this enricher.
    /// Empty if no dependencies.
    /// </summary>
    IReadOnlySet<string> DependsOn { get; }
}
