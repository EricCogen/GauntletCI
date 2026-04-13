// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0030 – IDisposable Resource Safety (superseded)
/// This rule's detection scope has been fully absorbed into GCI0024 (Resource Lifecycle),
/// which now covers both explicit known types and suffix-based heuristic detection.
/// GCI0030 returns no findings to prevent duplicate alerts. It is retained as a reserved
/// ID to avoid breaking configurations that reference it.
/// </summary>
public class GCI0030_DisposableResourceSafety : RuleBase
{
    public override string Id => "GCI0030";
    public override string Name => "IDisposable Resource Safety";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
        => Task.FromResult(new List<Finding>());
}
