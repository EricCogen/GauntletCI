// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Contract for a single GauntletCI rule that inspects a diff and returns zero or more findings.
/// Implementations are auto-discovered via reflection — no manual registration is required.
/// </summary>
public interface IRule
{
    /// <summary>The stable rule identifier (e.g. "GCI0001") used in config and output.</summary>
    string Id { get; }
    /// <summary>The human-readable display name shown in findings and reports.</summary>
    string Name { get; }
    /// <summary>
    /// Evaluates the rule against the provided analysis context and returns any findings.
    /// </summary>
    /// <param name="context">The full analysis context including the filtered diff and file records.</param>
    /// <param name="ct">Token used to cancel long-running or timed-out evaluations.</param>
    /// <returns>A list of findings; empty when no issues are detected.</returns>
    Task<List<Finding>> EvaluateAsync(AnalysisContext context, CancellationToken ct = default);
}
