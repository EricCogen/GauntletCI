// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0033 – Async Sinkhole (superseded)
/// This rule's detection of .Result and .Wait() blocking calls has been absorbed into
/// GCI0016 (Concurrency and State Risk), which already fires on the same patterns
/// with equivalent confidence and guidance. GCI0033 returns no findings to prevent
/// duplicate alerts. It is retained as a reserved ID to avoid breaking configurations.
/// </summary>
public class GCI0033_AsyncSinkhole : RuleBase
{
    public override string Id => "GCI0033";
    public override string Name => "Async Sinkhole";

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
        => Task.FromResult(new List<Finding>());
}
