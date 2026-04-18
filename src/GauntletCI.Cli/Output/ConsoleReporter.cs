// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
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
    public static void Report(EvaluationResult result, bool ascii = false, RuleSeverity minSeverity = RuleSeverity.Warn, int suppressedByBaseline = 0, DiffContext? diff = null, int showContext = 0)
    {
        string hr  = ascii ? "=======================================================" : "═══════════════════════════════════════════════════════";
        string sep = ascii ? "-- {0} ({1}) --------------------------" : "── {0} ({1}) ──────────────────────────";
        string ok  = ascii
            ? "  Scan complete. 0 detected signals. GauntletCI analyzes the diff only -- review context is still required."
            : "  ✓ Scan complete. 0 detected signals. GauntletCI analyzes the diff only — review context is still required.";

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
            if (suppressedByBaseline > 0)
                AnsiConsole.MarkupLine($"[dim]  ({suppressedByBaseline} finding(s) suppressed by baseline)[/]");
            return;
        }

        var groups = new[]
        {
            (RuleSeverity.Block, "POSSIBLE BLOCK", "red"),
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
                PrintFinding(finding, color, diff, showContext);
        }

        if (!anyVisible)
        {
            // Only print "below threshold" if there are also no Advisory findings being shown
            var advisoryFindings = result.Findings.Where(f => f.Severity == RuleSeverity.Advisory).ToList();
            if (advisoryFindings.Count == 0)
                AnsiConsole.MarkupLine("[grey]  All findings are below the current severity threshold. Use --verbose to see Info findings.[/]");

            if (advisoryFindings.Count > 0)
            {
                AnsiConsole.MarkupLine($"[blue]{string.Format(sep, "ENGINEERING POLICY SIGNALS", advisoryFindings.Count)}[/]");
                foreach (var finding in advisoryFindings)
                    PrintEpSignal(finding);
            }

            if (suppressedByBaseline > 0)
                AnsiConsole.MarkupLine($"[dim]  ({suppressedByBaseline} finding(s) suppressed by baseline)[/]");
            return;
        }

        // Advisory findings (LLM policy) — always shown, never gated by minSeverity
        var advisoryFindingsFinal = result.Findings.Where(f => f.Severity == RuleSeverity.Advisory).ToList();
        if (advisoryFindingsFinal.Count > 0)
        {
            AnsiConsole.MarkupLine($"[blue]{string.Format(sep, "ENGINEERING POLICY SIGNALS", advisoryFindingsFinal.Count)}[/]");
            foreach (var finding in advisoryFindingsFinal)
                PrintEpSignal(finding);
        }

        if (suppressedByBaseline > 0)
            AnsiConsole.MarkupLine($"[dim]  ({suppressedByBaseline} finding(s) suppressed by baseline)[/]");
    }

    /// <summary>
    /// Renders a single finding to the console, redacting evidence for sensitive rule IDs.
    /// </summary>
    /// <param name="finding">The finding to display.</param>
    /// <param name="accentColor">Spectre.Console color name applied to the rule ID and label.</param>
    private static void PrintFinding(Finding finding, string accentColor, DiffContext? diff = null, int showContext = 0)
    {
        AnsiConsole.MarkupLine($"[{accentColor}]  [[{finding.RuleId}]][/] [white]{Markup.Escape(finding.RuleName)}[/]");
        AnsiConsole.MarkupLine($"  Summary  : {Markup.Escape(finding.Summary)}");

        var evidenceDisplay = SensitiveRuleIds.Contains(finding.RuleId)
            ? MaskEvidenceSnippet(finding.Evidence)
            : finding.Evidence;
        AnsiConsole.MarkupLine($"[grey]  Evidence : {Markup.Escape(evidenceDisplay)}[/]");

        if (showContext > 0 && diff is not null && finding.FilePath is not null && finding.Line.HasValue)
        {
            var contextLines = GetDiffContext(diff, finding.FilePath, finding.Line.Value, showContext);
            if (contextLines.Count > 0)
            {
                AnsiConsole.MarkupLine("[grey]  Context  :[/]");
                foreach (var (prefix, content) in contextLines)
                {
                    var color = prefix == "+" ? "green" : prefix == "-" ? "red" : "grey";
                    AnsiConsole.MarkupLine($"[{color}]    {prefix} {Markup.Escape(content)}[/]");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(finding.CodeSnippet))
        {
            AnsiConsole.MarkupLine("[grey]  Snippet  :[/]");
            foreach (var line in finding.CodeSnippet.Split('\n'))
                AnsiConsole.MarkupLine($"[grey]    {Markup.Escape(line)}[/]");
        }

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

    /// <summary>Returns up to <paramref name="n"/> surrounding lines from the diff for a finding's file and line number.</summary>
    private static List<(string Prefix, string Content)> GetDiffContext(DiffContext diff, string filePath, int lineNumber, int n)
    {
        var diffFile = diff.Files.FirstOrDefault(f =>
            string.Equals(f.NewPath, filePath, StringComparison.OrdinalIgnoreCase) ||
            f.NewPath.EndsWith(filePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

        if (diffFile is null) return [];

        var allLines = diffFile.Hunks.SelectMany(h => h.Lines).ToList();
        var idx = allLines.FindIndex(l => l.LineNumber == lineNumber && l.Kind != DiffLineKind.Removed);
        if (idx < 0)
            idx = allLines.FindIndex(l => l.LineNumber == lineNumber);
        if (idx < 0) return [];

        var start = Math.Max(0, idx - n);
        var end   = Math.Min(allLines.Count - 1, idx + n);

        return allLines[start..(end + 1)].Select(l =>
        {
            var prefix = l.Kind == DiffLineKind.Added ? "+" : l.Kind == DiffLineKind.Removed ? "-" : " ";
            return (prefix, l.Content);
        }).ToList();
    }

    /// <summary>
    /// Renders an Engineering Policy signal in the structured Pattern / Evidence / Implication / Action format.
    /// </summary>
    private static void PrintEpSignal(Finding finding)
    {
        AnsiConsole.MarkupLine($"[blue]  [[{Markup.Escape(finding.RuleId)}: {Markup.Escape(finding.RuleName)}]] SIGNAL[/]");
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(finding.Summary))
        {
            AnsiConsole.MarkupLine("[white]  Pattern:[/]");
            AnsiConsole.MarkupLine($"  {Markup.Escape(finding.Summary)}");
            AnsiConsole.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(finding.Evidence))
        {
            AnsiConsole.MarkupLine("[white]  Evidence:[/]");
            foreach (var bullet in finding.Evidence.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                AnsiConsole.MarkupLine($"[grey]  - {Markup.Escape(bullet.Trim())}[/]");
            AnsiConsole.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(finding.WhyItMatters))
        {
            AnsiConsole.MarkupLine("[white]  Implication:[/]");
            AnsiConsole.MarkupLine($"  {Markup.Escape(finding.WhyItMatters)}");
            AnsiConsole.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(finding.SuggestedAction))
        {
            AnsiConsole.MarkupLine("[cyan]  Action:[/]");
            AnsiConsole.MarkupLine($"[cyan]  {Markup.Escape(finding.SuggestedAction)}[/]");
        }

        AnsiConsole.WriteLine();
    }
}
