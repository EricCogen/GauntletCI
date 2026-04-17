// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using Spectre.Console;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Pretty-prints <see cref="EvaluationResult"/> findings to the console, grouped by severity.
/// Block findings (red) and Warn findings (yellow) are shown by default.
/// Info findings (grey) are shown only when <paramref name="minSeverity"/> is <see cref="RuleSeverity.Info"/>
/// (i.e., when the caller passes <c>--verbose</c>).
/// </summary>
public static class ConsoleReporter
{
    /// <summary>
    /// Rules whose evidence may contain raw matched content (secrets, PII).
    /// For these, the code-snippet portion of the evidence is redacted in CLI output.
    /// </summary>
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

    /// <summary>
    /// Prints a formatted risk-analysis report to the console, grouped by severity level.
    /// Block (red), Warn (yellow), and Info (grey) findings are gated by minSeverity.
    /// Advisory findings (blue) from LLM policy evaluation are always shown regardless of minSeverity.
    /// </summary>
    /// <param name="result">The evaluation result containing findings to display.</param>
    /// <param name="ascii">Use ASCII box characters instead of Unicode for limited terminals.</param>
    /// <param name="minSeverity">Minimum severity to display. Defaults to <see cref="RuleSeverity.Warn"/>.</param>
    public static void Report(EvaluationResult result, bool ascii = false, RuleSeverity minSeverity = RuleSeverity.Warn)
    {
        string hr  = ascii ? "=======================================================" : "═══════════════════════════════════════════════════════";
        string sep = ascii ? "-- {0} ({1}) --------------------------" : "── {0} ({1}) ──────────────────────────";
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
            (RuleSeverity.Block, "BLOCK", "red"),
            (RuleSeverity.Warn,  "WARN",  "yellow"),
            (RuleSeverity.Info,  "INFO",  "grey"),
        };

        bool anyVisible = false;
        foreach (var (severity, label, color) in groups)
        {
            if (severity < minSeverity) continue;
            var findings = result.Findings.Where(f => f.Severity == severity).ToList();
            if (findings.Count == 0) continue;

            anyVisible = true;
            AnsiConsole.MarkupLine($"[{color}]{string.Format(sep, label, findings.Count)}[/]");
            foreach (var finding in findings)
                PrintFinding(finding, color);
        }

        if (!anyVisible)
        {
            // Only print "below threshold" if there are also no Advisory findings being shown
            var advisoryFindings = result.Findings.Where(f => f.Severity == RuleSeverity.Advisory).ToList();
            if (advisoryFindings.Count == 0)
                AnsiConsole.MarkupLine("[grey]  All findings are below the current severity threshold. Use --verbose to see Info findings.[/]");

            if (advisoryFindings.Count > 0)
            {
                AnsiConsole.MarkupLine($"[blue]{string.Format(sep, "ADVISORY", advisoryFindings.Count)}[/]");
                foreach (var finding in advisoryFindings)
                    PrintFinding(finding, "blue");
            }
            return;
        }

        // Advisory findings (LLM policy) — always shown, never gated by minSeverity
        var advisoryFindingsFinal = result.Findings.Where(f => f.Severity == RuleSeverity.Advisory).ToList();
        if (advisoryFindingsFinal.Count > 0)
        {
            AnsiConsole.MarkupLine($"[blue]{string.Format(sep, "ADVISORY", advisoryFindingsFinal.Count)}[/]");
            foreach (var finding in advisoryFindingsFinal)
                PrintFinding(finding, "blue");
        }
    }

    /// <summary>
    /// Renders a single finding to the console, redacting evidence for sensitive rule IDs.
    /// </summary>
    /// <param name="finding">The finding to display.</param>
    /// <param name="accentColor">Spectre.Console color name applied to the rule ID and label.</param>
    private static void PrintFinding(Finding finding, string accentColor)
    {
        AnsiConsole.MarkupLine($"[{accentColor}]  [[{finding.RuleId}]][/] [white]{Markup.Escape(finding.RuleName)}[/]");
        AnsiConsole.MarkupLine($"  Summary  : {Markup.Escape(finding.Summary)}");

        var evidenceDisplay = SensitiveRuleIds.Contains(finding.RuleId)
            ? MaskEvidenceSnippet(finding.Evidence)
            : finding.Evidence;
        AnsiConsole.MarkupLine($"[grey]  Evidence : {Markup.Escape(evidenceDisplay)}[/]");

        AnsiConsole.MarkupLine($"  Why      : {Markup.Escape(finding.WhyItMatters)}");
        AnsiConsole.MarkupLine($"[cyan]  Action   : {Markup.Escape(finding.SuggestedAction)}[/]");

        if (!string.IsNullOrEmpty(finding.LlmExplanation))
            AnsiConsole.MarkupLine($"[magenta]  LLM      : {Markup.Escape(finding.LlmExplanation)}[/]");

        if (finding.ExpertContext is { } expert)
        {
            AnsiConsole.MarkupLine($"[blue]  Expert   : {Markup.Escape(expert.Content)}[/]");
            AnsiConsole.MarkupLine($"[grey]             Score {expert.Score:F2} · {Markup.Escape(expert.Source)}[/]");
        }

        AnsiConsole.WriteLine();
    }
}
