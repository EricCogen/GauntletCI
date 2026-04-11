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
        var evidenceDisplay = SensitiveRuleIds.Contains(finding.RuleId)
            ? MaskEvidenceSnippet(finding.Evidence)
            : finding.Evidence;
        Console.WriteLine($"  Evidence : {evidenceDisplay}");
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
