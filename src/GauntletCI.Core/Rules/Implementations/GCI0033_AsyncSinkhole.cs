// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0033 – Async Sinkhole (superseded by GCI0016)
/// Retained as a reserved ID to avoid breaking configurations. Does not participate
/// in rule discovery — use GCI0016 (Concurrency and State Risk) for async blocking checks.
/// </summary>
// Superseded by GCI0016 (Concurrency and State Risk)
public class GCI0033_AsyncSinkhole
{
    public Task<List<Finding>> EvaluateAsync(
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        CancellationToken ct = default)
        => Task.FromResult(new List<Finding>());
}

