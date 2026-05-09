// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Calls the Anthropic Messages API to classify a rule finding as true/false positive.
/// Returns null on any HTTP or parse error.
/// </summary>
public sealed class AnthropicLlmLabeler : ILlmLabeler
{
    private readonly HttpClient _http;
    private readonly string     _model;

    public AnthropicLlmLabeler(string apiKey, string model = "claude-haiku-4-5")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Anthropic API key must not be empty.", nameof(apiKey));
        _model = model;
        _http  = HttpClientFactory.GetAnthropicClient();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
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
            var prompt = LlmLabelerHelpers.BuildPrompt(
                ruleId, findingMessage, evidence, filePath,
                LlmLabelerHelpers.TruncateComments(reviewCommentBodies),
                LlmLabelerHelpers.TruncateDiff(diffSnippet));

            var requestBody = JsonSerializer.Serialize(new
            {
                model      = _model,
                max_tokens = 150,
                messages   = new[] { new { role = "user", content = prompt } },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content)) return null;
            if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0) return null;

            var text = content[0].GetProperty("text").GetString();
            return string.IsNullOrWhiteSpace(text) ? null : LlmLabelerHelpers.ParseJson(text);
        }
        catch { return null; }
    }
}
