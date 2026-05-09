// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Corpus.Labeling;

/// <summary>Shared prompt-building and JSON-parsing logic for all LLM labeler implementations.</summary>
internal static class LlmLabelerHelpers
{
    internal static string BuildPrompt(
        string ruleId, string findingMessage, string evidence,
        string? filePath, string commentText, string diffSnippet) => $$"""
        You are evaluating whether a static analysis rule finding is a true positive on a code review.

        Rule ID: {{ruleId}}
        Finding: {{findingMessage}}
        Evidence: {{evidence}}
        File: {{filePath ?? "unknown"}}

        Review comments from human code reviewer on this pull request:
        {{commentText}}

        Diff snippet (first 800 chars):
        {{diffSnippet}}

        Is this rule finding a genuine risk in this pull request?
        Respond ONLY with valid JSON (no markdown, no explanation outside the JSON):
        {"should_trigger": true/false, "confidence": 0.0-1.0, "reason": "one sentence"}
        """;

    internal static LlmLabelResult? ParseJson(string text)
    {
        try
        {
            // Strip markdown fences if the model wraps the JSON
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```")) trimmed = trimmed.Split('\n', 2)[1];
            if (trimmed.EndsWith("```"))   trimmed = trimmed[..trimmed.LastIndexOf("```")];

            using var doc = JsonDocument.Parse(trimmed.Trim());
            var root = doc.RootElement;

            if (!root.TryGetProperty("should_trigger", out var shouldTrigger)) return null;
            if (!root.TryGetProperty("confidence",     out var confidence))     return null;
            if (!root.TryGetProperty("reason",         out var reason))         return null;

            var conf = confidence.GetDouble();
            return new LlmLabelResult(
                ShouldTrigger:  shouldTrigger.GetBoolean(),
                Confidence:     conf,
                Reason:         reason.GetString() ?? string.Empty,
                IsInconclusive: conf < 0.4);
        }
        catch (Exception) { return null; }
    }

    internal static string TruncateComments(IEnumerable<string> bodies, int maxChars = 500)
    {
        var text = string.Join("\n", bodies);
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    internal static string TruncateDiff(string diff, int maxChars = 800) =>
        diff.Length > maxChars ? diff[..maxChars] : diff;
}
