// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Calls the GitHub Models API (OpenAI-compatible) to classify a rule finding as true/false positive.
/// Uses GITHUB_TOKEN for authentication: no separate API key required.
/// Returns null on any HTTP or parse error.
/// </summary>
public sealed class GitHubModelsLlmLabeler : ILlmLabeler, IDisposable
{
    private const string Endpoint = "https://models.inference.ai.azure.com/chat/completions";

    private readonly HttpClient _http;
    private readonly string     _model;

    public GitHubModelsLlmLabeler(string githubToken, string model = "gpt-4o-mini")
    {
        _model = model;
        _http  = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", githubToken);
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
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
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

    public void Dispose() => _http.Dispose();
}
