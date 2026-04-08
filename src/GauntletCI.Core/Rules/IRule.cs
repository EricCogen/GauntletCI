// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules;

public interface IRule
{
    string Id { get; }
    string Name { get; }
    Task<List<Finding>> EvaluateAsync(DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default);
}
