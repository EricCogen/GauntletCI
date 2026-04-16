// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
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
    private readonly string     _baseUrl;

    public OllamaLlmLabeler(string model = "mistral", string baseUrl = "http://localhost:11434")
    {
        _model    = model;
        _baseUrl  = baseUrl.TrimEnd('/');
        _endpoint = $"{_baseUrl}/v1/chat/completions";
        _http     = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string BaseUrl => _baseUrl;

    // -----------------------------------------------------------------------
    // Readiness checks
    // -----------------------------------------------------------------------

    /// <summary>Returns true if the <c>ollama</c> CLI is on the PATH.</summary>
    public static bool IsInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo("ollama", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>Returns true if the Ollama HTTP server is responding.</summary>
    public async Task<bool> IsServerRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// Tries to start <c>ollama serve</c> in the background and waits up to
    /// <paramref name="timeoutSeconds"/> seconds for the server to become responsive.
    /// </summary>
    public async Task<bool> TryStartServerAsync(int timeoutSeconds = 15, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("ollama", "serve")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            Process.Start(psi); // fire-and-forget — Ollama manages its own lifecycle
        }
        catch { return false; }

        // Poll until the server responds or we time out
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000, ct);
            if (await IsServerRunningAsync(ct)) return true;
        }
        return false;
    }

    /// <summary>Returns true if <see cref="_model"/> is already pulled locally.</summary>
    public async Task<bool> IsModelAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("models", out var models)) return false;

            foreach (var m in models.EnumerateArray())
            {
                var name = m.GetProperty("name").GetString() ?? "";
                // "mistral:latest" should match "mistral"
                if (name.Equals(_model, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(_model + ":", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Runs <c>ollama pull &lt;model&gt;</c> and streams each output line to
    /// <paramref name="onProgress"/>. Returns true on success.
    /// </summary>
    public async Task<bool> TryPullModelAsync(Action<string>? onProgress = null, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("ollama", $"pull {_model}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = Process.Start(psi)!;

            // Read stdout and stderr concurrently to avoid deadlocks
            var stdoutTask = ReadLinesAsync(proc.StandardOutput, onProgress, ct);
            var stderrTask = ReadLinesAsync(proc.StandardError,  onProgress, ct);

            await Task.WhenAll(stdoutTask, stderrTask);
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    // -----------------------------------------------------------------------
    // Classification
    // -----------------------------------------------------------------------

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

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task ReadLinesAsync(
        System.IO.TextReader reader, Action<string>? onLine, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!string.IsNullOrWhiteSpace(line)) onLine?.Invoke(line);
        }
    }

    public void Dispose() => _http.Dispose();
}

