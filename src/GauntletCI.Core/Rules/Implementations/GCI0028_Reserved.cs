// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0028 – Reserved ID
/// This ID was skipped during rule development and is retained as a stub to avoid
/// breaking any configurations that reference it. No findings are ever produced.
/// </summary>
public class GCI0028_Reserved
{
    public Task<List<Finding>> EvaluateAsync(
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        CancellationToken ct = default)
        => Task.FromResult(new List<Finding>());
}
