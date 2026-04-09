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

            var file = ExtractFile(finding.Evidence);
            var line = ExtractLine(finding.Evidence);
            var title = $"{finding.RuleId} {finding.RuleName}";
            var message = finding.Summary.Replace("\n", "%0A").Replace("\r", "");

            var annotation = string.IsNullOrEmpty(file)
                ? $"::{level} title={title}::{message}"
                : $"::{level} file={file},line={line},title={title}::{message}";

            Console.WriteLine(annotation);
        }
    }

    private static string ExtractFile(string? evidence)
    {
        if (string.IsNullOrEmpty(evidence)) return string.Empty;
        // Evidence often starts with "Line N: ..." or contains a file path
        // Try to find a path-like token (contains / or \)
        var parts = evidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(p =>
            (p.Contains('/') || p.Contains('\\')) &&
            !p.StartsWith("Line", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static int ExtractLine(string? evidence)
    {
        if (string.IsNullOrEmpty(evidence)) return 1;
        // "Line 42: ..." pattern
        var idx = evidence.IndexOf("Line ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 1;
        var rest = evidence[(idx + 5)..];
        var numStr = new string(rest.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(numStr, out var n) ? n : 1;
    }
}
