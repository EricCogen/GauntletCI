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
    // LLM input cap default — overridden by EngineeringPolicyConfig.MaxDiffChars.
    // At ~4 chars/token this is ~3000 tokens, well within a 16K context window.

    private static readonly HashSet<string> CanonicalRuleIds =
    [
        "EP_SCOPE", "EP_CONTRACTS", "EP_OBSERVABILITY",
        "EP_FAILURE", "EP_TESTING", "EP_CORRECTNESS"
    ];

    private static readonly Dictionary<string, string> CanonicalRuleNames = new()
    {
        ["EP_SCOPE"]         = "Scope and Containment",
        ["EP_CONTRACTS"]     = "Contracts and Compatibility",
        ["EP_OBSERVABILITY"] = "Observability and Diagnosability",
        ["EP_FAILURE"]       = "Failure Handling",
        ["EP_TESTING"]       = "Testing and Verification",
        ["EP_CORRECTNESS"]   = "Correctness and Intent",
    };

    /// <summary>
    /// Evaluates the diff against the policy file at <paramref name="policyPath"/> using the provided LLM.
    /// Returns an empty list if the LLM is unavailable, the policy file is missing, or no violations are found.
    /// Large diffs (over <see cref="MaxDiffChars"/> chars) are rejected unless <paramref name="isLicensed"/> is true.
    /// </summary>
    internal static async Task<IReadOnlyList<Finding>> EvaluateAsync(
        DiffContext diff,
        string policyPath,
        ILlmEngine llm,
        bool isLicensed = false,
        int maxDiffChars = 12_000,
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

        var rawDiffText = BuildRawDiffText(diff);
        if (rawDiffText.Length > maxDiffChars && !isLicensed)
        {
            Console.Error.WriteLine(
                $"[GauntletCI] Engineering policy skipped: diff is {rawDiffText.Length:N0} chars " +
                $"(community limit: {maxDiffChars:N0}). Upgrade to Business or Enterprise to evaluate large diffs.");
            return [];
        }

        var diffText  = rawDiffText.Length > maxDiffChars
            ? rawDiffText[..maxDiffChars] + "\n... (truncated)"
            : rawDiffText;
        var fileNames = ExtractFileNames(diffText);

        string raw;
        try
        {
            raw = await llm.CompleteAsync(BuildUserMessage(diffText, fileNames), BuildSystemPrompt(policy), ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Engineering policy LLM call failed: {ex.Message}");
            return [];
        }

        return ParseFindings(raw, fileNames);
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

    private static List<string> ExtractFileNames(string diffText) =>
        diffText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("// FILE: ", StringComparison.Ordinal))
            .Select(l => l["// FILE: ".Length..].Trim())
            .SelectMany(p => new[] { p, Path.GetFileName(p) })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BuildRawDiffText(DiffContext diff)
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
        return sb.ToString();
    }

    private static string BuildSystemPrompt(string policy) => $"""
        You are a code reviewer evaluating git diffs against an engineering policy.
        Enforce only the invariants listed in the policy below. Return ONLY valid JSON — no explanation, no markdown fences.

        ## Rules
        - Only report violations you have high confidence in. Report at most 5 findings.
        - Use hedged language: "likely", "may", "appears to", "could indicate".
        - Evidence must reference a real filename from the diff, not a placeholder.
        - Do not mention AI, LLM, model, or inference.

        ## Engineering Policy
        {policy}
        """;

    private static string BuildUserMessage(string diffText, List<string> fileNames)
    {
        var fileHint = fileNames.Count > 0
            ? $"\nFiles changed: {string.Join(", ", fileNames.Where(f => f.Contains('.', StringComparison.Ordinal)).Take(8))}"
            : string.Empty;

        return $$"""
            ## Diff (production code only){{fileHint}}
            {{diffText}}

            ## Instructions
            Review the diff against each engineering invariant in your policy.
            Return a JSON array of up to 5 violations, or [] if none.

            IMPORTANT: Evidence MUST reference a real filename from the "Files changed" list above.
            If you cannot find evidence in those specific files, do not include the finding.
            Output [] if no violations exist in these files.

            Each element must have these fields:
            - "ruleId": short tag like "EP_SCOPE", "EP_CONTRACTS", "EP_OBSERVABILITY", "EP_FAILURE", "EP_TESTING", "EP_CORRECTNESS"
            - "ruleName": the exact policy area name from above
            - "summary": one hedged sentence describing the specific issue you saw in the diff
            - "evidence": MUST name a file from the list above and what you observed (e.g. "Foo.cs — exception swallowed in catch block")
            - "whyItMatters": one sentence on the engineering risk
            - "suggestedAction": one concrete fix
            """;
    }

    private static IReadOnlyList<Finding> ParseFindings(string raw, List<string> knownFiles)
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
                .Where(r => !string.IsNullOrWhiteSpace(r.RuleId)
                         && !string.IsNullOrWhiteSpace(r.Summary)
                         && !string.IsNullOrWhiteSpace(r.Evidence)
                         && EvidenceReferencesKnownFile(r.Evidence!, knownFiles))
                .Select(r =>
                {
                    var evidenceBullets = string.Join("\n", r.Evidence!
                        .Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0));

                    var normalizedId = NormalizeRuleId(r.RuleId!.TrimEnd(':', ' '));
                    return new Finding
                    {
                        RuleId          = normalizedId,
                        RuleName        = CanonicalRuleNames[normalizedId],
                        Summary         = r.Summary!,
                        Evidence        = evidenceBullets,
                        WhyItMatters    = r.WhyItMatters ?? string.Empty,
                        SuggestedAction = r.SuggestedAction ?? string.Empty,
                        Severity        = RuleSeverity.Advisory,
                        Confidence      = Confidence.Medium,
                    };
                }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Maps any ruleId the LLM emits to the nearest canonical EP_ tag.
    /// Non-canonical values are normalised by keyword matching; unknown → EP_CORRECTNESS.
    /// </summary>
    private static string NormalizeRuleId(string raw)
    {
        var upper = raw.ToUpperInvariant();
        if (CanonicalRuleIds.Contains(upper)) return upper;

        if (upper.Contains("OBSERV") || upper.Contains("DIAGNOS") || upper.Contains("LOG") || upper.Contains("MONITOR"))
            return "EP_OBSERVABILITY";
        if (upper.Contains("TEST") || upper.Contains("SPEC") || upper.Contains("VERIF"))
            return "EP_TESTING";
        if (upper.Contains("CONTRACT") || upper.Contains("COMPAT") || upper.Contains("BREAK") || upper.Contains("API"))
            return "EP_CONTRACTS";
        if (upper.Contains("FAIL") || upper.Contains("ERROR") || upper.Contains("EXCEPT") || upper.Contains("RECOVER"))
            return "EP_FAILURE";
        if (upper.Contains("SCOPE") || upper.Contains("COMPLEX") || upper.Contains("CONTAIN"))
            return "EP_SCOPE";

        return "EP_CORRECTNESS";
    }

    /// <summary>
    /// Returns true if evidence references at least one known file, or if no known files exist
    /// (e.g. stdin diff with no FILE markers — skip validation in that case).
    /// </summary>
    private static bool EvidenceReferencesKnownFile(string evidence, List<string> knownFiles) =>
        knownFiles.Count == 0
        || knownFiles.Any(f => evidence.Contains(f, StringComparison.OrdinalIgnoreCase));

    private sealed record PolicyFinding(
        string? RuleId,
        string? RuleName,
        string? Summary,
        string? Evidence,
        string? WhyItMatters,
        string? SuggestedAction);
}
