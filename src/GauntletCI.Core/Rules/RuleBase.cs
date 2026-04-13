// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

public abstract class RuleBase : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }

    public abstract Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default);

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

    protected Finding CreateFinding(
        DiffFile file,
        string summary,
        string evidence,
        string whyItMatters,
        string suggestedAction,
        Confidence confidence,
        DiffLine? line = null) =>
        new()
        {
            RuleId          = Id,
            RuleName        = Name,
            Summary         = summary,
            Evidence        = evidence,
            WhyItMatters    = whyItMatters,
            SuggestedAction = suggestedAction,
            Confidence      = confidence,
            FilePath        = file.NewPath,
            Line            = (line?.LineNumber > 0) ? line.LineNumber : null,
        };
}
