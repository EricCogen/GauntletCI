// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core.Model;
namespace GauntletCI.Cli.TicketProviders;

public sealed class JiraTicketProvider : ITicketProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static JiraTicketProvider() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("GauntletCI/2.0");

    public string ProviderName => "Jira";

    public bool IsAvailable =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JIRA_BASE_URL")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JIRA_API_TOKEN")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JIRA_USER_EMAIL"));

    public async Task<TicketInfo?> FetchAsync(string issueKey, CancellationToken ct = default)
    {
        var baseUrl = Environment.GetEnvironmentVariable("JIRA_BASE_URL")!.TrimEnd('/');
        var token   = Environment.GetEnvironmentVariable("JIRA_API_TOKEN")!;
        var email   = Environment.GetEnvironmentVariable("JIRA_USER_EMAIL")!;
        var creds   = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));

        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/rest/api/3/issue/{issueKey}?fields=summary,description");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var fields = doc.RootElement.GetProperty("fields");
        var summary = fields.TryGetProperty("summary", out var s) ? s.GetString() : null;
        var desc    = ExtractDescription(fields);

        return new TicketInfo
        {
            Id          = issueKey,
            Title       = summary ?? issueKey,
            Description = desc,
            Url         = $"{baseUrl}/browse/{issueKey}",
            Provider    = "Jira",
        };
    }

    private static string? ExtractDescription(JsonElement fields)
    {
        if (!fields.TryGetProperty("description", out var d) || d.ValueKind == JsonValueKind.Null)
            return null;
        // Jira v3 uses Atlassian Document Format; extract plain text from paragraphs
        if (d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (!block.TryGetProperty("content", out var inner) ||
                    inner.ValueKind != JsonValueKind.Array) continue;
                foreach (var node in inner.EnumerateArray())
                {
                    if (node.TryGetProperty("text", out var t)) sb.Append(t.GetString()).Append(' ');
                }
            }
            var text = sb.ToString().Trim();
            return text.Length > 500 ? text[..500] : text;
        }
        // Fallback: treat as plain string
        var raw = d.GetString();
        return raw?.Length > 500 ? raw[..500] : raw;
    }
}
