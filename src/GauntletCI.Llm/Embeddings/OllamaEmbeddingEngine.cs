// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// Embedding engine backed by Ollama's /api/embeddings endpoint.
/// Default model: nomic-embed-text (pull with: ollama pull nomic-embed-text)
/// Default URL: http://localhost:11434
/// </summary>
public sealed class OllamaEmbeddingEngine : IEmbeddingEngine, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly bool _ownsHttpClient;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public bool IsAvailable => true;

    public OllamaEmbeddingEngine(
        string model = "nomic-embed-text",
        string baseUrl = "http://localhost:11434",
        HttpClient? http = null)
    {
        _model    = model;
        _endpoint = baseUrl.TrimEnd('/') + "/api/embeddings";
        if (http is not null)
        {
            _http = http;
            _ownsHttpClient = false;
        }
        else
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
            _ownsHttpClient = true;
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { model = _model, prompt = text });
        using var content  = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var json   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(json, JsonOpts);
        return result?.Embedding ?? [];
    }

    public void Dispose() { if (_ownsHttpClient) _http.Dispose(); }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")] public float[]? Embedding { get; init; }
    }
}
