// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules;

public abstract class RuleBase : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }

    public abstract Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default);

    protected Finding CreateFinding(
        string summary,
        string evidence,
        string whyItMatters,
        string suggestedAction,
        Confidence confidence) =>
        new()
        {
            RuleId = Id,
            RuleName = Name,
            Summary = summary,
            Evidence = evidence,
            WhyItMatters = whyItMatters,
            SuggestedAction = suggestedAction,
            Confidence = confidence
        };
}
