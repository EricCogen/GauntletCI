// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

public interface IRule
{
    string Id { get; }
    string Name { get; }
    Task<List<Finding>> EvaluateAsync(AnalysisContext context, CancellationToken ct = default);
}
