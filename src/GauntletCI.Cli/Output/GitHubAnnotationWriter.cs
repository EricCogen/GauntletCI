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
    /// Writes one GitHub Actions annotation command per finding to stdout.
    /// </summary>
    /// <param name="result">The evaluation result whose findings are annotated.</param>
    public static void Write(EvaluationResult result)
    {
        foreach (var finding in result.Findings)
        {
            var level = finding.Confidence switch
            {
                Confidence.High   => "error",
                Confidence.Medium => "warning",
                _                 => "notice",
            };

            var file = finding.FilePath ?? string.Empty;
            var line = finding.Line ?? 1;
            var title = $"{finding.RuleId} {finding.RuleName}"
                .Replace("%", "%25")
                .Replace(",", "%2C")
                .Replace(":", "%3A")
                .Replace("\r", "")
                .Replace("\n", "");

            var message = BuildMessage(finding);

            var annotation = string.IsNullOrEmpty(file)
                ? $"::{level} title={title}::{message}"
                : $"::{level} file={file},line={line},title={title}::{message}";

            Console.WriteLine(annotation);
        }
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
