// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0030 – IDisposable Resource Safety (superseded by GCI0024)
/// Retained as a reserved ID to avoid breaking configurations. Does not participate
/// in rule discovery — use GCI0024 (Resource Lifecycle) for disposable safety checks.
/// </summary>
// Superseded by GCI0024 (Resource Lifecycle)
public class GCI0030_DisposableResourceSafety
{
    public Task<List<Finding>> EvaluateAsync(
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        CancellationToken ct = default)
        => Task.FromResult(new List<Finding>());
}
