using System.Text.Json;

namespace GauntletCI.PrIntegration;

public static class GitHubWebhookParser
{
    public static PrEventContext ParsePullRequestEvent(string payloadJson, string workingDirectory)
    {
        using JsonDocument doc = JsonDocument.Parse(payloadJson);
        JsonElement root = doc.RootElement;

        JsonElement repo = root.GetProperty("repository");
        JsonElement pr = root.GetProperty("pull_request");

        string owner = repo.GetProperty("owner").GetProperty("login").GetString() ?? throw new InvalidOperationException("Missing repository owner.");
        string repository = repo.GetProperty("name").GetString() ?? throw new InvalidOperationException("Missing repository name.");
        int number = root.GetProperty("number").GetInt32();
        string headSha = pr.GetProperty("head").GetProperty("sha").GetString() ?? throw new InvalidOperationException("Missing PR head SHA.");

        return new PrEventContext(owner, repository, number, headSha, workingDirectory);
    }
}

public sealed class PrIntegrationHost(HttpClient httpClient)
{
    public async Task<PrEvaluationSummary> ProcessWebhookAsync(string payloadJson, string workingDirectory, CancellationToken cancellationToken)
    {
        string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GITHUB_TOKEN must be set for PR integration.");
        }

        PrEventContext context = GitHubWebhookParser.ParsePullRequestEvent(payloadJson, workingDirectory);
        IGitHubClient github = new GitHubApiClient(httpClient, token);
        PrReviewOrchestrator orchestrator = new(github);
        return await orchestrator.EvaluateAndPublishAsync(context, cancellationToken).ConfigureAwait(false);
    }
}
