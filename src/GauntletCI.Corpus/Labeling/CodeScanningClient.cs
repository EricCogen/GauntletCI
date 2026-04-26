// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// HTTP client for the GitHub Code Scanning API.
/// Requires a GITHUB_TOKEN environment variable for authenticated requests
/// (5,000 req/hr vs 60 unauthenticated; code scanning API requires auth even for public repos).
/// </summary>
public sealed class CodeScanningClient : IDisposable
{
    private const string BaseUrl = "https://api.github.com";

    private readonly HttpClient _http;

    public CodeScanningClient()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Returns whether this client has a GitHub token configured.
    /// Code scanning alerts require authentication.
    /// </summary>
    public bool IsAuthenticated =>
        _http.DefaultRequestHeaders.Contains("Authorization");

    /// <summary>
    /// Fetches all open code scanning alerts for <paramref name="repo"/> (format: "owner/repo").
    /// Optionally filters to a specific <paramref name="toolName"/> (default: "CodeQL").
    /// Returns an empty list if the repo has no code scanning enabled (HTTP 404).
    /// Paginates using GitHub's 100-per-page limit with a small courtesy delay between pages.
    /// </summary>
    public async Task<IReadOnlyList<CodeScanningAlert>> GetAlertsAsync(
        string repo,
        string toolName = "CodeQL",
        CancellationToken ct = default)
    {
        var results = new List<CodeScanningAlert>();
        int page = 1;

        while (true)
        {
            var url = $"{BaseUrl}/repos/{repo}/code-scanning/alerts"
                    + $"?state=open"
                    + $"&tool_name={Uri.EscapeDataString(toolName)}"
                    + $"&per_page=100&page={page}";

            try
            {
                using var resp = await _http.GetAsync(url, ct);

                // 404 = code scanning not enabled; 403 = token lacks security_events scope
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    break;

                if (!resp.IsSuccessStatusCode) break;

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                if (doc.RootElement.ValueKind != JsonValueKind.Array) break;

                int countThisPage = 0;
                foreach (var alert in doc.RootElement.EnumerateArray())
                {
                    var mapped = MapAlert(alert, repo);
                    if (mapped is not null)
                    {
                        results.Add(mapped);
                        countThisPage++;
                    }
                }

                if (countThisPage < 100) break; // last page
                page++;
                await Task.Delay(100, ct); // polite delay - well within 5k/hr rate limit
            }
            catch (OperationCanceledException) { throw; }
            catch { break; }
        }

        return results;
    }

    private static CodeScanningAlert? MapAlert(JsonElement alert, string repo)
    {
        // most_recent_instance.location.path
        if (!alert.TryGetProperty("most_recent_instance", out var instance)) return null;
        if (!instance.TryGetProperty("location", out var location)) return null;
        if (!location.TryGetProperty("path", out var pathEl)) return null;
        var filePath = pathEl.GetString() ?? "";
        if (string.IsNullOrEmpty(filePath)) return null;

        alert.TryGetProperty("rule",  out var rule);
        alert.TryGetProperty("tool",  out var tool);
        alert.TryGetProperty("state", out var stateEl);

        var startLine = 0;
        if (location.TryGetProperty("start_line", out var slEl))
            startLine = slEl.ValueKind == JsonValueKind.Number ? slEl.GetInt32() : 0;

        var message = "";
        if (instance.TryGetProperty("message", out var msgObj) &&
            msgObj.TryGetProperty("text", out var msgText))
            message = msgText.GetString() ?? "";

        return new CodeScanningAlert
        {
            Repo      = repo,
            FilePath  = filePath,
            RuleId    = rule.ValueKind != JsonValueKind.Undefined && rule.TryGetProperty("id",   out var rid)  ? rid.GetString()  ?? "" : "",
            RuleName  = rule.ValueKind != JsonValueKind.Undefined && rule.TryGetProperty("name", out var rn)   ? rn.GetString()   ?? "" : "",
            Severity  = rule.ValueKind != JsonValueKind.Undefined && rule.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "" : "",
            State     = stateEl.ValueKind != JsonValueKind.Undefined ? stateEl.GetString() ?? "" : "",
            ToolName  = tool.ValueKind != JsonValueKind.Undefined && tool.TryGetProperty("name", out var tn)  ? tn.GetString()   ?? "" : "",
            Message   = message,
            StartLine = startLine,
        };
    }
}
