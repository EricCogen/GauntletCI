// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using Spectre.Console;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Pretty-prints <see cref="EvaluationResult"/> findings to the console with colors.
/// Findings are grouped by confidence: High first, then Medium, then Low.
/// Pass ascii: true for terminals that cannot render Unicode box-drawing characters.
/// </summary>
public static class ConsoleReporter
{
    // Rules whose Evidence may contain sensitive matched content (secrets, PII).
    // For these, the code snippet portion is redacted in CLI output — only file/line is shown.
    private static readonly HashSet<string> SensitiveRuleIds = ["GCI0012", "GCI0029"];

    /// <summary>
    /// Masks the code-snippet portion of an evidence string, keeping only the file/line reference.
    /// e.g. "Line 42: _logger.Log(user.Email)" → "Line 42: [REDACTED]"
    /// e.g. "src/Auth.cs:42" → unchanged (no snippet present)
    /// </summary>
    public static string MaskEvidenceSnippet(string evidence)
    {
        var idx = evidence.IndexOf(": ", StringComparison.Ordinal);
        return idx >= 0 ? $"{evidence[..(idx + 2)]}[REDACTED]" : evidence;
    }

    public static void Report(EvaluationResult result, bool ascii = false)
    {
        string hr  = ascii ? "=======================================================" : "═══════════════════════════════════════════════════════";
        string sep = ascii ? "-- {0} CONFIDENCE ({1}) --------------------------" : "── {0} CONFIDENCE ({1}) ──────────────────────────";
        string ok  = ascii ? "  OK No findings -- diff looks clean!" : "  ✓ No findings — diff looks clean!";

        AnsiConsole.MarkupLine($"[cyan]{hr}[/]");
        AnsiConsole.MarkupLine("[cyan]  GauntletCI Risk Analysis Report[/]");
        AnsiConsole.MarkupLine($"[cyan]{hr}[/]");

        if (!string.IsNullOrEmpty(result.CommitSha))
            AnsiConsole.MarkupLine($"  Commit : {result.CommitSha}");

        AnsiConsole.MarkupLine($"  Rules  : {result.RulesEvaluated} evaluated");
        AnsiConsole.MarkupLine($"  Findings: {result.Findings.Count}");
        AnsiConsole.WriteLine();

        if (!result.HasFindings)
        {
            AnsiConsole.MarkupLine($"[green]{ok}[/]");
            return;
        }

        var groups = new[]
        {
            (Confidence.High,   "HIGH",   "red"),
            (Confidence.Medium, "MEDIUM", "yellow"),
            (Confidence.Low,    "LOW",    "darkorange"),
        };

        foreach (var (confidence, label, color) in groups)
        {
            var findings = result.Findings.Where(f => f.Confidence == confidence).ToList();
            if (findings.Count == 0) continue;

            AnsiConsole.MarkupLine($"[{color}]{string.Format(sep, label, findings.Count)}[/]");

            foreach (var finding in findings)
                PrintFinding(finding, color);
        }
    }

    private static void PrintFinding(Finding finding, string accentColor)
    {
        AnsiConsole.MarkupLine($"[{accentColor}]  [[{finding.RuleId}]][/] [white]{finding.RuleName}[/]");
        AnsiConsole.MarkupLine($"  Summary  : {finding.Summary}");

        var evidenceDisplay = SensitiveRuleIds.Contains(finding.RuleId)
            ? MaskEvidenceSnippet(finding.Evidence)
            : finding.Evidence;
        AnsiConsole.MarkupLine($"[grey]  Evidence : {Markup.Escape(evidenceDisplay)}[/]");

        AnsiConsole.MarkupLine($"  Why      : {Markup.Escape(finding.WhyItMatters)}");
        AnsiConsole.MarkupLine($"[cyan]  Action   : {Markup.Escape(finding.SuggestedAction)}[/]");

        if (!string.IsNullOrEmpty(finding.LlmExplanation))
            AnsiConsole.MarkupLine($"[magenta]  LLM      : {Markup.Escape(finding.LlmExplanation)}[/]");

        AnsiConsole.WriteLine();
    }
}
