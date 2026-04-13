// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Emits GitHub Actions workflow commands for inline PR annotations.
/// Format: ::error file={path},line={line},title={title}::{message}
/// </summary>
public static class GitHubAnnotationWriter
{
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
            var message = finding.Summary.Replace("\n", "%0A").Replace("\r", "");

            var annotation = string.IsNullOrEmpty(file)
                ? $"::{level} title={title}::{message}"
                : $"::{level} file={file},line={line},title={title}::{message}";

            Console.WriteLine(annotation);
        }
    }
}
