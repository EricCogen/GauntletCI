// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Calls the Anthropic Messages API to classify a rule finding as true/false positive.
/// Returns null on any HTTP or parse error.
/// </summary>
public sealed class AnthropicLlmLabeler : ILlmLabeler, IDisposable
{
    private readonly HttpClient _http;

    public AnthropicLlmLabeler(string apiKey)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LlmLabelResult?> ClassifyAsync(
        string ruleId,
        string findingMessage,
        string evidence,
        string? filePath,
        IEnumerable<string> reviewCommentBodies,
        string diffSnippet,
        CancellationToken ct = default)
    {
        try
        {
            var commentText = string.Join("\n", reviewCommentBodies);
            if (commentText.Length > 500) commentText = commentText[..500];

            var snippet = diffSnippet.Length > 800 ? diffSnippet[..800] : diffSnippet;

            var prompt = $$"""
                You are evaluating whether a static analysis rule finding is a true positive on a code review.

                Rule ID: {{ruleId}}
                Finding: {{findingMessage}}
                Evidence: {{evidence}}
                File: {{filePath ?? "unknown"}}

                Review comments from human code reviewer on this pull request:
                {{commentText}}

                Diff snippet (first 800 chars):
                {{snippet}}

                Is this rule finding a genuine risk in this pull request?
                Respond ONLY with valid JSON (no markdown, no explanation outside the JSON):
                {"should_trigger": true/false, "confidence": 0.0-1.0, "reason": "one sentence"}
                """;

            var requestBody = JsonSerializer.Serialize(new
            {
                model = "claude-haiku-4-5",
                max_tokens = 150,
                messages = new[] { new { role = "user", content = prompt } },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content)) return null;
            if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0) return null;

            var text = content[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text)) return null;

            return ParseLlmJson(text);
        }
        catch
        {
            return null;
        }
    }

    private static LlmLabelResult? ParseLlmJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("should_trigger", out var shouldTrigger)) return null;
            if (!root.TryGetProperty("confidence", out var confidence)) return null;
            if (!root.TryGetProperty("reason", out var reason)) return null;

            var conf = confidence.GetDouble();
            return new LlmLabelResult(
                ShouldTrigger: shouldTrigger.GetBoolean(),
                Confidence: conf,
                Reason: reason.GetString() ?? string.Empty,
                IsInconclusive: conf < 0.4);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
