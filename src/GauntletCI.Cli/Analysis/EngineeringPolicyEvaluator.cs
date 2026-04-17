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

        var policy = await File.ReadAllTextAsync(policyPath, ct);
        var diffText = BuildDiffText(diff);

        var prompt = BuildPrompt(policy, diffText);

        string raw;
        try
        {
            raw = await llm.CompleteAsync(prompt, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Engineering policy LLM call failed: {ex.Message}");
            return [];
        }

        return ParseFindings(raw);
    }

    private static string BuildDiffText(DiffContext diff)
    {
        var lines = diff.Files
            .SelectMany(f => f.Hunks)
            .SelectMany(h => h.Lines)
            .Where(l => l.Kind == DiffLineKind.Added)
            .Select(l => l.Content);

        var text = string.Join("\n", lines);
        return text.Length > MaxDiffChars ? text[..MaxDiffChars] + "\n... (truncated)" : text;
    }

    private static string BuildPrompt(string policy, string diffText) => $$"""
        You are a code reviewer evaluating a git diff against an engineering policy.

        ## Engineering Policy
        {{policy}}

        ## Diff (added lines only)
        {{diffText}}

        ## Instructions
        Review the diff against each engineering invariant above.
        For each violation, return a JSON array. Return [] if there are no violations.
        Return ONLY valid JSON — no explanation, no markdown fences.

        Schema:
        [
          {
            "ruleId": "EP004",
            "ruleName": "Failure Handling",
            "summary": "One sentence describing the violation.",
            "evidence": "FileName.cs:42 — the offending snippet",
            "whyItMatters": "One sentence on the risk.",
            "suggestedAction": "One sentence on how to fix it."
          }
        ]
        """;

    private static IReadOnlyList<Finding> ParseFindings(string raw)
    {
        try
        {
            var trimmed = raw.Trim();

            // Strip markdown fences if the model wrapped the JSON anyway
            if (trimmed.StartsWith("```"))
            {
                var start = trimmed.IndexOf('[');
                var end   = trimmed.LastIndexOf(']');
                if (start >= 0 && end > start)
                    trimmed = trimmed[start..(end + 1)];
            }

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
        string? WhyItMatters,
        string? SuggestedAction);
}
