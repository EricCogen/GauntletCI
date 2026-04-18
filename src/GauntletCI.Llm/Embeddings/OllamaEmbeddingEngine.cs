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

    /// <summary>Always <see langword="true"/>; reachability of the Ollama server is not pre-checked.</summary>
    public bool IsAvailable => true;

    /// <summary>Initializes the engine and configures the HTTP endpoint for the specified model.</summary>
    /// <param name="model">Ollama model name to use for embeddings (e.g., <c>nomic-embed-text</c>).</param>
    /// <param name="baseUrl">Base URL of the Ollama server (default: <c>http://localhost:11434</c>).</param>
    /// <param name="http">Optional pre-configured <see cref="HttpClient"/>; a new one is created when <see langword="null"/>.</param>
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
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
            _ownsHttpClient = true;
        }
    }

    /// <summary>Posts the text to Ollama's embeddings API and returns the resulting float vector.</summary>
    /// <param name="text">Input text to embed; should fit within the model's context window.</param>
    /// <param name="ct">Token used to cancel the HTTP request.</param>
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

    /// <summary>Disposes the <see cref="HttpClient"/> when this instance owns it.</summary>
    public void Dispose() { if (_ownsHttpClient) _http.Dispose(); }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")] public float[]? Embedding { get; init; }
    }
}
