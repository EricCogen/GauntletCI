// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Cli.Analysis;

/// <summary>
/// Evaluates a diff against the structured engineering policy document using an LLM.
/// Produces Advisory-severity findings for any detected policy violations.
/// </summary>
internal static class EngineeringPolicyEvaluator
{
    private const int MaxDiffChars = 12_000;

    /// <summary>
    /// Evaluates the diff against the policy file at <paramref name="policyPath"/> using the provided LLM.
    /// Returns an empty list if the LLM is unavailable, the policy file is missing, or no violations are found.
    /// </summary>
    internal static async Task<IReadOnlyList<Finding>> EvaluateAsync(
        DiffContext diff,
        string policyPath,
        ILlmEngine llm,
        CancellationToken ct = default)
    {
        if (!llm.IsAvailable)
            return [];

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"[GauntletCI] Engineering policy file not found: {policyPath}. Skipping policy evaluation.");
            return [];
        }

        string policy;
        try
        {
            policy = await File.ReadAllTextAsync(policyPath, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Engineering policy file could not be read: {ex.Message}. Skipping policy evaluation.");
            return [];
        }
        var diffText = BuildDiffText(diff);

        string raw;
        try
        {
            raw = await llm.CompleteAsync(BuildUserMessage(diffText), BuildSystemPrompt(policy), ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Engineering policy LLM call failed: {ex.Message}");
            return [];
        }

        return ParseFindings(raw);
    }

    private static readonly HashSet<string> TestPathMarkers =
    [
        "test", "tests", "spec", "specs", "unittest", "unittests", "integrationtest", "integrationtests"
    ];

    private static bool IsTestFile(string path)
    {
        var lower = path.Replace('\\', '/').ToLowerInvariant();
        return lower.EndsWith(".tests.cs", StringComparison.Ordinal)
            || lower.EndsWith("test.cs", StringComparison.Ordinal)
            || lower.EndsWith("tests.cs", StringComparison.Ordinal)
            || lower.EndsWith("spec.cs", StringComparison.Ordinal)
            || lower.EndsWith("specs.cs", StringComparison.Ordinal)
            || lower.Split('/').Any(seg => TestPathMarkers.Contains(seg));
    }

    private static string BuildDiffText(DiffContext diff)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var file in diff.Files)
        {
            var isTest = IsTestFile(file.NewPath ?? file.OldPath ?? "");
            var prefix = isTest ? "[TEST FILE] " : "";
            foreach (var hunk in file.Hunks)
            foreach (var line in hunk.Lines.Where(l => l.Kind == DiffLineKind.Added))
                sb.AppendLine(prefix + line.Content);
        }

        var text = sb.ToString();
        return text.Length > MaxDiffChars ? text[..MaxDiffChars] + "\n... (truncated)" : text;
    }

    private static string BuildSystemPrompt(string policy) => $"""
        You are a code reviewer evaluating git diffs against an engineering policy.
        Enforce only the invariants listed in the policy below. Return ONLY valid JSON — no explanation, no markdown fences.

        ## Important guidance
        - Lines prefixed with [TEST FILE] come from test code. Be very conservative — most engineering policy
          invariants (null guards, logging, resource cleanup, error propagation) do not apply to test code.
          Only flag a test file if there is a clear and serious violation that would affect test reliability.
        - These are advisory observations, not proven facts. Use appropriately hedged language in your output:
          prefer "likely", "probably", "may", "appears to", "could indicate" over absolute assertions.
        - Only report violations you have high confidence in. Prefer fewer, high-quality findings over many uncertain ones.

        ## Engineering Policy
        {policy}
        """;

    private static string BuildUserMessage(string diffText) => $$"""
        ## Diff (added lines only; [TEST FILE] prefix marks test code)
        {{diffText}}

        ## Instructions
        Review the diff against each engineering invariant in your policy.
        For each violation, return a JSON array. Return [] if there are no violations.

        Schema:
        [
          {
            "ruleId": "EP004",
            "ruleName": "Failure Handling",
            "summary": "One hedged sentence describing the likely violation (use 'likely', 'may', 'appears to', etc.).",
            "evidence": "FileName.cs:42 — brief description of the location",
            "codeSnippet": "// The verbatim offending line(s) from the diff, max 5 lines",
            "whyItMatters": "One sentence on the probable risk.",
            "suggestedAction": "One sentence on how to fix it."
          }
        ]
        """;

    private static IReadOnlyList<Finding> ParseFindings(string raw)
    {
        try
        {
            var trimmed = raw.Trim();

            // Extract JSON array regardless of preamble text or markdown fences
            var start = trimmed.IndexOf('[');
            var end   = trimmed.LastIndexOf(']');
            if (start >= 0 && end > start)
                trimmed = trimmed[start..(end + 1)];

            var records = JsonSerializer.Deserialize<PolicyFinding[]>(trimmed,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (records is null || records.Length == 0)
                return [];

            return records.Select(r => new Finding
            {
                RuleId          = r.RuleId ?? "EP000",
                RuleName        = r.RuleName ?? "Engineering Policy",
                Summary         = r.Summary ?? string.Empty,
                Evidence        = r.Evidence ?? string.Empty,
                CodeSnippet     = string.IsNullOrWhiteSpace(r.CodeSnippet) ? null : r.CodeSnippet.Trim(),
                WhyItMatters    = r.WhyItMatters ?? string.Empty,
                SuggestedAction = r.SuggestedAction ?? string.Empty,
                Severity        = RuleSeverity.Advisory,
                Confidence      = Confidence.Medium,
            }).ToList();
        }
        catch
        {
            // LLM returned non-JSON — skip silently
            return [];
        }
    }

    private sealed record PolicyFinding(
        string? RuleId,
        string? RuleName,
        string? Summary,
        string? Evidence,
        string? CodeSnippet,
        string? WhyItMatters,
        string? SuggestedAction);
}
