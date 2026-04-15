// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Calls a local Ollama instance (OpenAI-compatible /v1/chat/completions) to classify
/// a rule finding as true/false positive. No API key required.
/// Returns null on any HTTP or parse error (e.g., Ollama not running).
/// </summary>
public sealed class OllamaLlmLabeler : ILlmLabeler, IDisposable
{
    private readonly HttpClient _http;
    private readonly string     _model;
    private readonly string     _endpoint;

    public OllamaLlmLabeler(string model = "mistral", string baseUrl = "http://localhost:11434")
    {
        _model    = model;
        _endpoint = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";
        _http     = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
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
                messages   = new[] { new { role = "user", content = prompt } },
                max_tokens = 150,
                stream     = false,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return null;

            var text = choices[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(text) ? null : LlmLabelerHelpers.ParseJson(text);
        }
        catch { return null; }
    }

    /// <summary>Checks if Ollama is reachable at the configured base URL.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var baseUrl = _endpoint[..(_endpoint.LastIndexOf("/v1"))];
            using var resp = await _http.GetAsync($"{baseUrl}/api/tags", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}
