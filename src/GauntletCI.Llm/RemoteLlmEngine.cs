// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core;
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// Premium LLM engine for CI/CD. Calls any OpenAI-compatible chat completions endpoint.
/// Used when a license key and ci_endpoint are configured in .gauntletci.json and
/// GauntletCI is running in a CI environment.
/// </summary>
public sealed class RemoteLlmEngine : ILlmEngine
{
    private const int MaxEnrichTokens  = 256;    // single-sentence enrichment
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(120);

    private readonly string _endpoint;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly int _maxCompleteTokens;
    private readonly int _numCtx;
    private readonly HttpClient _http;

    /// <summary>Initializes the engine and configures the <see cref="HttpClient"/> with auth headers.</summary>
    /// <param name="endpoint">Full URL of the OpenAI-compatible chat completions endpoint.</param>
    /// <param name="model">Model identifier sent in each request body (e.g., <c>gpt-4o</c>).</param>
    /// <param name="apiKey">Bearer token used for authorization.</param>
    /// <param name="numCtx">Ollama context window in tokens (input + output). Default: 16384.</param>
    /// <param name="maxCompleteTokens">Max tokens the model may generate per call. Default: 2048.</param>
    public RemoteLlmEngine(string endpoint, string model, string apiKey,
        int numCtx = 16_384, int maxCompleteTokens = 2_048)
    {
        _endpoint           = endpoint;
        _model              = model;
        _apiKey             = apiKey;
        _numCtx             = numCtx;
        _maxCompleteTokens  = maxCompleteTokens;
        _http               = HttpClientFactory.GetLongTimeoutClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    /// <summary>Always <see langword="true"/>; reachability of the remote endpoint is not pre-checked.</summary>
    public bool IsAvailable => true;

    /// <summary>Builds an enrichment prompt and forwards it to the remote model.</summary>
    public async Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.EnrichFinding(
            finding.RuleId, finding.RuleName, finding.Summary, finding.Evidence);

        return await CallAsync(prompt, systemPrompt: null, MaxEnrichTokens, ct).ConfigureAwait(false);
    }

    /// <summary>Builds a summarization prompt from all finding summaries and forwards it to the remote model.</summary>
    public async Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.SummarizeReport(findings.Select(f => f.Summary));
        return await CallAsync(prompt, systemPrompt: null, MaxEnrichTokens, ct).ConfigureAwait(false);
    }

    /// <summary>Forwards a pre-built prompt directly to the remote model and returns its completion.</summary>
    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => CallAsync(prompt, systemPrompt: null, _maxCompleteTokens, ct);

    /// <summary>Forwards a prompt with an optional system message to the remote model.</summary>
    public Task<string> CompleteAsync(string prompt, string? systemPrompt, CancellationToken ct = default)
        => CallAsync(prompt, systemPrompt, _maxCompleteTokens, ct);

    private async Task<string> CallAsync(string userPrompt, string? systemPrompt, int maxTokens, CancellationToken ct)
    {
        object[] messages = systemPrompt is not null
            ? [new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt }]
            : [new { role = "user", content = userPrompt }];

        var body = new
        {
            model       = _model,
            max_tokens  = maxTokens,
            temperature = 0,
            seed        = 42,
            messages,
            options     = new { num_ctx = _numCtx, repeat_penalty = 1.1, top_k = 1 },
        };

        try
        {
            using var resp = await _http.PostAsJsonAsync(_endpoint, body, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Remote LLM error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>Disposes the underlying <see cref="HttpClient"/>.</summary>
    public void Dispose() => _http.Dispose();
}
