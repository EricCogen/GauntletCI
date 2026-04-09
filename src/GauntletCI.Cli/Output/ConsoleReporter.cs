// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Pretty-prints <see cref="EvaluationResult"/> findings to the console with colors.
/// Findings are grouped by confidence: High first, then Medium, then Low.
/// Pass ascii: true for terminals that cannot render Unicode box-drawing characters.
/// </summary>
public static class ConsoleReporter
{
    public static void Report(EvaluationResult result, bool ascii = false)
    {
        var originalColor = Console.ForegroundColor;

        string hr  = ascii ? "=======================================================" : "═══════════════════════════════════════════════════════";
        string sep = ascii ? "-- {0} CONFIDENCE ({1}) --------------------------" : "── {0} CONFIDENCE ({1}) ──────────────────────────";
        string ok  = ascii ? "  OK No findings -- diff looks clean!" : "  ✓ No findings — diff looks clean!";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(hr);
        Console.WriteLine("  GauntletCI Risk Analysis Report");
        Console.WriteLine(hr);
        Console.ResetColor();

        if (!string.IsNullOrEmpty(result.CommitSha))
            Console.WriteLine($"  Commit : {result.CommitSha}");

        Console.WriteLine($"  Rules  : {result.RulesEvaluated} evaluated");
        Console.WriteLine($"  Findings: {result.Findings.Count}");
        Console.WriteLine();

        if (!result.HasFindings)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(ok);
            Console.ResetColor();
            return;
        }

        var groups = new[]
        {
            (Confidence.High,   "HIGH",   ConsoleColor.Red),
            (Confidence.Medium, "MEDIUM", ConsoleColor.Yellow),
            (Confidence.Low,    "LOW",    ConsoleColor.DarkYellow),
        };

        foreach (var (confidence, label, color) in groups)
        {
            var findings = result.Findings.Where(f => f.Confidence == confidence).ToList();
            if (findings.Count == 0) continue;

            Console.ForegroundColor = color;
            Console.WriteLine(string.Format(sep, label, findings.Count));
            Console.ResetColor();

            foreach (var finding in findings)
                PrintFinding(finding, color);
        }

        Console.ForegroundColor = originalColor;
    }

    private static void PrintFinding(Finding finding, ConsoleColor accentColor)
    {
        Console.ForegroundColor = accentColor;
        Console.Write($"  [{finding.RuleId}] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(finding.RuleName);
        Console.ResetColor();

        Console.WriteLine($"  Summary  : {finding.Summary}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Evidence : {finding.Evidence}");
        Console.ResetColor();
        Console.WriteLine($"  Why      : {finding.WhyItMatters}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Action   : {finding.SuggestedAction}");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(finding.LlmExplanation))
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"  LLM      : {finding.LlmExplanation}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
