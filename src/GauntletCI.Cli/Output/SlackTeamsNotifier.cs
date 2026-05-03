// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Posts Block-severity findings to Slack and/or Microsoft Teams via incoming webhooks.
/// Soft-fails on missing webhooks or API errors.
/// 
/// Thread-Safe: Static HttpClient is thread-safe and managed by HttpClientFactory.
/// The factory uses Lazy&lt;T&gt; with ExecutionAndPublication semantics, ensuring
/// single-threaded initialization and safe concurrent access.
/// </summary>
public static class SlackTeamsNotifier
{
    /// <summary>
    /// Shared HTTP client for all notification requests.
    /// Thread-safe: managed by HttpClientFactory with lazy initialization.
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    private static readonly HttpClient _http = HttpClientFactory.GetGenericClient();

    /// <summary>
    /// Sends notifications to Slack and/or Teams when Block-severity findings exist.
    /// Notifications are sent in parallel when both URLs are configured.
    /// </summary>
    public static async Task NotifyAsync(
        EvaluationResult result,
        string? slackUrl,
        string? teamsUrl,
        CancellationToken ct = default)
    {
        var blockFindings = result.Findings.Where(f => f.Severity == RuleSeverity.Block).ToList();
        if (blockFindings.Count == 0)
            return;

        var repo  = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var sha   = Environment.GetEnvironmentVariable("GITHUB_SHA");
        var prNum = ResolvePrNumber();
        var shortSha = !string.IsNullOrEmpty(sha) && sha.Length >= 8 ? sha[..8] : sha ?? "unknown";
        var prNumStr = prNum?.ToString(); // Convert int? to string?

        var tasks = new List<Task>();
        
        if (slackUrl is not null)
            tasks.Add(SendSlackNotificationAsync(result, repo, prNumStr, shortSha, slackUrl, ct));
        
        if (teamsUrl is not null)
            tasks.Add(SendTeamsNotificationAsync(result, repo, prNumStr, shortSha, teamsUrl, ct));
        
        // Send notifications in parallel (both at once if both URLs exist)
        await Task.WhenAll(tasks);
    }

    private static async Task SendSlackNotificationAsync(
        EvaluationResult result, 
        string? repo, 
        string? prNum, 
        string shortSha, 
        string slackUrl,
        CancellationToken ct)
    {
        try
        {
            var payload = BuildSlackPayload(result, repo, prNum, shortSha);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(slackUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                Console.Error.WriteLine($"[GauntletCI] Slack notification failed: {response.StatusCode}: {body}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Slack notification error: {ex.Message}");
        }
    }

    private static async Task SendTeamsNotificationAsync(
        EvaluationResult result, 
        string? repo, 
        string? prNum, 
        string shortSha, 
        string teamsUrl,
        CancellationToken ct)
    {
        try
        {
            var payload = BuildTeamsPayload(result, repo, prNum, shortSha);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(teamsUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                Console.Error.WriteLine($"[GauntletCI] Teams notification failed: {response.StatusCode}: {body}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Teams notification error: {ex.Message}");
        }
    }

    internal static string BuildSlackPayload(
        EvaluationResult result,
        string? repo,
        string? prNum,
        string? sha)
    {
        var blockFindings = result.Findings.Where(f => f.Severity == RuleSeverity.Block).ToList();
        if (blockFindings.Count == 0)
            return string.Empty;

        var blocks = new List<object>
        {
            new { type = "header", text = new { type = "plain_text", text = "🚨 GauntletCI: High-Risk Changes Detected", emoji = true } },
            new { type = "section", text = new { type = "mrkdwn", text = $"*Repo:* {repo ?? "unknown"} | *PR:* #{prNum ?? "unknown"} | *Commit:* `{sha ?? "unknown"}`" } },
            new { type = "divider" },
        };

        var top3 = blockFindings.Take(3).ToList();
        foreach (var f in top3)
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = $"*[{f.RuleId}]* {f.RuleName}\n_{f.Summary}_\n> {f.Evidence}" } });
            blocks.Add(new { type = "divider" });
        }

        if (blockFindings.Count > 3)
            blocks.Add(new { type = "context", elements = new[] { new { type = "mrkdwn", text = $"...and {blockFindings.Count - 3} more Block findings" } } });

        return JsonSerializer.Serialize(new { blocks });
    }

    internal static string BuildTeamsPayload(
        EvaluationResult result,
        string? repo,
        string? prNum,
        string? sha)
    {
        var blockFindings = result.Findings.Where(f => f.Severity == RuleSeverity.Block).ToList();
        var findingsText = string.Join("\n", blockFindings.Select(f => $"**[{f.RuleId}]** {f.RuleName}: {f.Summary}"));

        var payload = new
        {
            @type    = "MessageCard",
            @context = "https://schema.org/extensions",
            themeColor = "FF0000",
            summary  = "GauntletCI Alert",
            title    = "🚨 GauntletCI: High-Risk PR",
            sections = new[]
            {
                new
                {
                    facts = new object[]
                    {
                        new { name = "Repo",   value = repo  ?? "unknown" },
                        new { name = "PR",     value = $"#{prNum ?? "unknown"}" },
                        new { name = "Commit", value = sha   ?? "unknown" },
                    },
                    text = $"**Block findings:**\n{findingsText}",
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string? ResolvePrNumber()
    {
        var explicit_ = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        if (!string.IsNullOrEmpty(explicit_))
            return explicit_;

        var ghRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        if (!string.IsNullOrEmpty(ghRef))
        {
            var parts = ghRef.Split('/');
            if (parts.Length >= 4 && parts[1] == "pull")
                return parts[2];
        }

        return null;
    }
}
