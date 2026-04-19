// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Model;
namespace GauntletCI.Cli.TicketProviders;

public static class TicketResolver
{
    // Jira: e.g. PROJ-1234, COMP-456 (uppercase letters + digits, dash, digits)
    // Note: GH-42 matches this pattern (treating "GH" as a Jira project key) — Jira takes priority.
    private static readonly Regex JiraKey = new(@"\b([A-Z][A-Z0-9]+-\d+)\b", RegexOptions.Compiled);
    // Linear: e.g. eng-123, team-456 (lowercase letters, dash, digits)
    private static readonly Regex LinearKey = new(@"\b([a-z][a-z0-9]+-\d+)\b", RegexOptions.Compiled);
    // GitHub: #42 (GH-42 is handled by Jira regex above)
    private static readonly Regex GitHubKey = new(@"(?:#|GH-)(\d+)\b", RegexOptions.Compiled);

    /// <summary>
    /// Extracts a single issue key from a branch name or PR body text.
    /// Priority: Jira > Linear > GitHub. Returns null if none found.
    /// </summary>
    public static (string? Key, string? Provider) DetectIssueKey(string? branchName, string? prBody)
    {
        var text = $"{branchName} {prBody}".Trim();
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        var jira = JiraKey.Match(text);
        if (jira.Success) return (jira.Groups[1].Value, "Jira");

        var linear = LinearKey.Match(text);
        if (linear.Success) return (linear.Groups[1].Value, "Linear");

        var gh = GitHubKey.Match(text);
        if (gh.Success) return (gh.Groups[1].Value, "GitHub");

        return (null, null);
    }

    /// <summary>
    /// Resolves an available provider for the detected provider name.
    /// Returns null if no provider is available.
    /// </summary>
    public static ITicketProvider? ResolveProvider(string? providerName)
    {
        return providerName switch
        {
            "Jira"   => new JiraTicketProvider(),
            "Linear" => new LinearTicketProvider(),
            "GitHub" => new GitHubIssueProvider(),
            _        => null
        };
    }

    /// <summary>
    /// Detects issue key, fetches ticket, and attaches TicketContext to all findings.
    /// Soft-fails: logs to stderr on error, never throws.
    /// </summary>
    public static async Task AnnotateFindingsAsync(
        string? branchName,
        IReadOnlyList<Finding> findings,
        CancellationToken ct = default)
    {
        var prBody = Environment.GetEnvironmentVariable("GITHUB_PR_BODY");
        var (key, providerName) = DetectIssueKey(branchName, prBody);
        if (key is null) return;

        var provider = ResolveProvider(providerName);
        if (provider is null || !provider.IsAvailable)
        {
            Console.Error.WriteLine($"[ticket] Detected {providerName} key {key} but no credentials found for {providerName}.");
            return;
        }

        try
        {
            var ticket = await provider.FetchAsync(key, ct);
            if (ticket is null)
            {
                Console.Error.WriteLine($"[ticket] Could not fetch {key} from {providerName}.");
                return;
            }
            foreach (var finding in findings)
                finding.TicketContext = ticket;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ticket] Error fetching {key}: {ex.Message}");
        }
    }
}
