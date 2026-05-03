// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Core.Model;
namespace GauntletCI.Cli.TicketProviders;

public sealed class GitHubIssueProvider : ITicketProvider
{
    private static readonly HttpClient Http = HttpClientFactory.GetGitHubClient();

    public string ProviderName => "GitHub";
    public bool IsAvailable =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY"));

    public async Task<TicketInfo?> FetchAsync(string issueKey, CancellationToken ct = default)
    {
        var token      = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(repository))
        {
            return null;  // Not available
        }
        
        // issueKey may be "#42" or "42"
        var number = issueKey.TrimStart('#');

        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{repository}/issues/{number}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
        var body  = doc.RootElement.TryGetProperty("body",  out var b) ? b.GetString() : null;
        var url   = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;

        return new TicketInfo
        {
            Id          = $"#{number}",
            Title       = title ?? $"#{number}",
            Description = body?.Length > 500 ? body[..500] : body,
            Url         = url,
            Provider    = "GitHub",
        };
    }
}
