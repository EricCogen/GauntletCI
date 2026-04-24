// SPDX-License-Identifier: Elastic-2.0
using System.Text;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Emits GitHub Actions workflow commands for inline PR annotations.
/// Format: ::error file={path},line={line},title={title}::{message}
/// LlmExplanation and ExpertContext are appended to the message when present.
/// </summary>
public static class GitHubAnnotationWriter
{
    /// <summary>
    /// Writes one GitHub Actions annotation command per grouped finding to stdout.
    /// Findings sharing (RuleId, FilePath) are collapsed via <see cref="FindingGrouper"/> so a
    /// rule that fires on multiple lines produces a single annotation summarising all hits.
    /// </summary>
    /// <param name="result">The evaluation result whose findings are annotated.</param>
    public static void Write(EvaluationResult result)
    {
        foreach (var group in FindingGrouper.Group(result.Findings))
        {
            var level = group.Confidence switch
            {
                Confidence.High   => "error",
                Confidence.Medium => "warning",
                _                 => "notice",
            };

            var file = group.FilePath ?? string.Empty;
            var line = group.PrimaryLine ?? 1;
            var title = $"{group.RuleId} {group.RuleName}"
                .Replace("%", "%25")
                .Replace(",", "%2C")
                .Replace(":", "%3A")
                .Replace("\r", "")
                .Replace("\n", "");

            var message = BuildMessage(group);

            var annotation = string.IsNullOrEmpty(file)
                ? $"::{level} title={title}::{message}"
                : $"::{level} file={file},line={line},title={title}::{message}";

            Console.WriteLine(annotation);
        }
    }

    /// <summary>
    /// Builds a multi-line annotation message body from a grouped finding. Lines are joined with
    /// <c>%0A</c> so GitHub renders them as separate lines in the annotation popover. Mirrors the
    /// run-log console layout: Summary / Lines / Evidence / Why / Action (+ optional LLM/Expert).
    /// </summary>
    public static string BuildMessage(GroupedFinding group)
    {
        var sb = new StringBuilder();
        sb.Append(Sanitize(group.Summary));

        if (group.Lines.Count > 1)
            sb.Append("%0A").Append("Lines: ").Append(string.Join(", ", group.Lines));

        if (group.Evidence.Count > 0)
        {
            sb.Append("%0A").Append("Evidence:");
            foreach (var ev in group.Evidence)
                sb.Append("%0A  - ").Append(Sanitize(ev));
        }

        if (!string.IsNullOrWhiteSpace(group.WhyItMatters))
            sb.Append("%0A").Append("Why: ").Append(Sanitize(group.WhyItMatters));

        if (!string.IsNullOrWhiteSpace(group.SuggestedAction))
            sb.Append("%0A").Append("Action: ").Append(Sanitize(group.SuggestedAction));

        if (!string.IsNullOrWhiteSpace(group.LlmExplanation))
            sb.Append("%0A").Append("LLM: ").Append(Sanitize(group.LlmExplanation!));

        if (group.ExpertContext is { } ctx)
            sb.Append("%0A").Append("Expert: ")
              .Append(Sanitize(ctx.Content)).Append(" (").Append(Sanitize(ctx.Source)).Append(')');

        return sb.ToString();
    }

    /// <summary>
    /// Builds the annotation message body, appending LLM explanation and expert context when present.
    /// </summary>
    /// <param name="finding">The finding whose summary and enrichment data is serialised.</param>
    /// <returns>A single-line string safe for use inside a GitHub Actions workflow command.</returns>
    public static string BuildMessage(Finding finding)
    {
        var sb = new StringBuilder();
        sb.Append(Sanitize(finding.Summary));

        if (!string.IsNullOrWhiteSpace(finding.LlmExplanation))
            sb.Append($" | LLM: {Sanitize(finding.LlmExplanation!)}");

        if (finding.ExpertContext is { } ctx)
            sb.Append($" | Expert: {Sanitize(ctx.Content)} ({Sanitize(ctx.Source)})");

        return sb.ToString();
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", "").Replace("\n", "%0A");
}
