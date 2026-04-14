// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Base class for all GauntletCI rules. Provides shared <see cref="CreateFinding"/> helpers
/// so concrete rules do not need to repeat boilerplate property assignments.
/// </summary>
public abstract class RuleBase : IRule
{
    /// <inheritdoc/>
    public abstract string Id { get; }
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default);

    /// <summary>
    /// Creates a finding without file or line attribution, for diff-wide or commit-level detections.
    /// </summary>
    /// <param name="summary">A short description of the detected risk.</param>
    /// <param name="evidence">The code snippet or context that triggered the rule.</param>
    /// <param name="whyItMatters">Why this pattern is risky.</param>
    /// <param name="suggestedAction">Actionable guidance for the author.</param>
    /// <param name="confidence">The rule's confidence in this finding.</param>
    /// <returns>A fully populated <see cref="Finding"/> attributed to this rule.</returns>
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

    /// <summary>
    /// Creates a finding attributed to a specific file and optional line within that file.
    /// </summary>
    /// <param name="file">The changed file where the issue was detected.</param>
    /// <param name="summary">A short description of the detected risk.</param>
    /// <param name="evidence">The code snippet or context that triggered the rule.</param>
    /// <param name="whyItMatters">Why this pattern is risky.</param>
    /// <param name="suggestedAction">Actionable guidance for the author.</param>
    /// <param name="confidence">The rule's confidence in this finding.</param>
    /// <param name="line">The specific diff line that triggered the finding, or <c>null</c> for file-level findings.</param>
    /// <returns>A fully populated <see cref="Finding"/> with file and optional line attribution.</returns>
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
