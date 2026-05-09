// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Core.Model;
namespace GauntletCI.Cli.TicketProviders;

public sealed class LinearTicketProvider : ITicketProvider
{
    private static readonly HttpClient Http = HttpClientFactory.GetGenericClient();

    public string ProviderName => "Linear";
    public bool IsAvailable
    {
        get
        {
            var key = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
            return !string.IsNullOrEmpty(key);
        }
    }

    public async Task<TicketInfo?> FetchAsync(string issueKey, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;  // Provider not available
        }
        var query  = new { query = "query($id:String!){issue(id:$id){id title description url}}", variables = new { id = issueKey } };
        var body   = JsonSerializer.Serialize(query);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.linear.app/graphql")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;  // Defensive: apiKey cleared after earlier check
            
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (!data.TryGetProperty("issue", out var issue) || issue.ValueKind == JsonValueKind.Null) return null;

        var title = issue.TryGetProperty("title", out var t) ? t.GetString() : null;
        var desc  = issue.TryGetProperty("description", out var d) ? d.GetString() : null;
        var url   = issue.TryGetProperty("url", out var u) ? u.GetString() : null;

        return new TicketInfo
        {
            Id          = issueKey,
            Title       = title ?? issueKey,
            Description = desc?.Length > 500 ? desc[..500] : desc,
            Url         = url,
            Provider    = "Linear",
        };
    }
}
