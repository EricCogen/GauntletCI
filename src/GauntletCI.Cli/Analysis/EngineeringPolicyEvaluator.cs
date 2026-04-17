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
            var path = file.NewPath ?? file.OldPath ?? "unknown";
            if (IsTestFile(path)) continue;  // test files never ship; skip entirely
            sb.AppendLine($"// FILE: {path}");
            foreach (var hunk in file.Hunks)
            foreach (var line in hunk.Lines.Where(l => l.Kind == DiffLineKind.Added))
                sb.AppendLine(line.Content);
            sb.AppendLine();
        }

        var text = sb.ToString();
        return text.Length > MaxDiffChars ? text[..MaxDiffChars] + "\n... (truncated)" : text;
    }

    private static string BuildSystemPrompt(string policy) => $"""
        You are an engineering policy evaluation engine. You assess git diffs against a structured policy.
        Return ONLY a valid JSON array — no explanation, no markdown fences, no prose.

        ## Output rules
        - Pattern: 3 to 8 words, noun phrase, no hedging. Example: "Failure-handling path weakened"
        - Evidence: plain string. Separate multiple observations with " | ". Reference file:line when available.
        - Implication: 1 to 2 sentences. Use "may", "can", or "could" — not "likely" or "probably".
        - Action: One concrete engineering follow-up. Must be actionable.
        - Do not mention AI, LLM, model, or inference anywhere in the output.
        - All code shown is production code only. Test files are never included.
        - Only report violations you have high confidence in. Prefer fewer, high-quality findings.

        ## Engineering Policy
        {policy}
        """;

    private static string BuildUserMessage(string diffText) => $$"""
        ## Diff (production code only)
        {{diffText}}

        ## Instructions
        Evaluate the diff against each engineering invariant in your policy.
        For each violation, return a JSON array. Return [] if there are no violations.

        Schema (evidence is a plain string; use " | " to separate multiple observations):
        [
          {
            "ruleId": "EP004",
            "ruleName": "Failure Handling",
            "pattern": "Failure-handling path weakened",
            "evidence": "LoggingBus.cs:42 — continuation does not publish error on failure | RemoveLogger called without prior error publish",
            "implication": "Failure conditions may not be surfaced or propagated correctly at runtime.",
            "action": "Verify that all failure paths still raise or propagate exceptions as intended."
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

            return records
                .Where(r => !string.IsNullOrWhiteSpace(r.RuleId) && !string.IsNullOrWhiteSpace(r.Pattern))
                .Select(r =>
                {
                    // Normalize "|"-separated evidence into newline-separated bullets for the renderer
                    var evidenceBullets = string.IsNullOrWhiteSpace(r.Evidence)
                        ? string.Empty
                        : string.Join("\n", r.Evidence
                            .Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 0));

                    return new Finding
                    {
                        RuleId          = r.RuleId!,
                        RuleName        = r.RuleName ?? "Engineering Policy",
                        Summary         = r.Pattern!,
                        Evidence        = evidenceBullets,
                        WhyItMatters    = r.Implication ?? string.Empty,
                        SuggestedAction = r.Action ?? string.Empty,
                        Severity        = RuleSeverity.Advisory,
                        Confidence      = Confidence.Medium,
                    };
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
        string? Pattern,
        string? Evidence,
        string? Implication,
        string? Action);
}
