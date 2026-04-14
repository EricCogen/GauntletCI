// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// Premium LLM engine for CI/CD. Calls any OpenAI-compatible chat completions endpoint.
/// Used when a license key and ci_endpoint are configured in .gauntletci.json and
/// GauntletCI is running in a CI environment.
/// </summary>
public sealed class RemoteLlmEngine : ILlmEngine
{
    private const int MaxFindingTokens = 256;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly string _endpoint;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public RemoteLlmEngine(string endpoint, string model, string apiKey)
    {
        _endpoint = endpoint;
        _model    = model;
        _apiKey   = apiKey;
        _http     = new HttpClient { Timeout = RequestTimeout };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
    }

    public bool IsAvailable => true;

    public async Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.EnrichFinding(
            finding.RuleId, finding.RuleName, finding.Summary, finding.Evidence);

        return await CallAsync(prompt, ct);
    }

    public async Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.SummarizeReport(findings.Select(f => f.Summary));
        return await CallAsync(prompt, ct);
    }

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => CallAsync(prompt, ct);

    private async Task<string> CallAsync(string userPrompt, CancellationToken ct)
    {
        var body = new
        {
            model       = _model,
            max_tokens  = MaxFindingTokens,
            temperature = 0,
            messages    = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        try
        {
            using var resp = await _http.PostAsJsonAsync(_endpoint, body, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
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

    public void Dispose() => _http.Dispose();
}
