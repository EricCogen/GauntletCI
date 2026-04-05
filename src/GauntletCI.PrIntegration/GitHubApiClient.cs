using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core.Models;

namespace GauntletCI.PrIntegration;

public interface IGitHubClient
{
    Task<string> GetPullRequestDiffAsync(PrEventContext context, CancellationToken cancellationToken);

    Task PublishReviewCommentAsync(PrEventContext context, string filePath, int line, string body, CancellationToken cancellationToken);

    Task PublishStatusCheckAsync(PrEventContext context, string verdict, EvaluationResult result, CancellationToken cancellationToken);
}

public sealed class GitHubApiClient(HttpClient httpClient, string token) : IGitHubClient
{
    public async Task<string> GetPullRequestDiffAsync(PrEventContext context, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{context.Owner}/{context.Repository}/pulls/{context.PullRequestNumber}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task PublishReviewCommentAsync(PrEventContext context, string filePath, int line, string body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, $"https://api.github.com/repos/{context.Owner}/{context.Repository}/pulls/{context.PullRequestNumber}/comments");
        var payload = new
        {
            body,
            commit_id = context.HeadSha,
            path = filePath,
            line,
            side = "RIGHT",
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.IsSuccessStatusCode;
    }

    public async Task PublishStatusCheckAsync(PrEventContext context, string verdict, EvaluationResult result, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, $"https://api.github.com/repos/{context.Owner}/{context.Repository}/check-runs");
        var payload = new
        {
            name = "GauntletCI",
            head_sha = context.HeadSha,
            status = "completed",
            conclusion = result.ExitCode == 0 ? "success" : "failure",
            output = new
            {
                title = $"GauntletCI verdict: {verdict}",
                summary = $"Findings: {result.Findings.Count}; Duration: {result.EvaluationDurationMs} ms",
                text = string.Join("\n", result.Findings.Select(static finding => $"- {finding.RuleId}: {finding.FindingText}")),
            },
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.IsSuccessStatusCode;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GauntletCI", "0.1.0"));
        return request;
    }
}
