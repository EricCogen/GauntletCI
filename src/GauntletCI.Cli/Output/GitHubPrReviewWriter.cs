// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Posts GauntletCI findings as a GitHub Pull Request review with inline comments.
/// Requires GITHUB_TOKEN, GITHUB_REPOSITORY, and either GAUNTLETCI_PR_NUMBER or GITHUB_REF.
/// The calling workflow must declare <c>pull-requests: write</c> permission.
/// Soft-fails with a stderr warning if prerequisites are missing or the API call fails.
/// </summary>
public static class GitHubPrReviewWriter
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    /// <summary>
    /// Posts findings as a GitHub PR review. Soft-fails on missing env vars or API errors.
    /// If inline comments are rejected (422), retries as a summary-only review.
    /// </summary>
    public static async Task WriteAsync(EvaluationResult result, CancellationToken ct = default)
    {
        if (result.Findings.Count == 0)
            return;

        var githubAuth = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        // Prefer explicit override so callers can pass the PR head SHA directly.
        var sha        = Environment.GetEnvironmentVariable("GAUNTLETCI_COMMIT_SHA")
                      ?? Environment.GetEnvironmentVariable("GITHUB_SHA");
        var prNumber   = ResolvePrNumber();

        if (string.IsNullOrEmpty(githubAuth) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(sha))
        {
            Console.Error.WriteLine(
                "[GauntletCI] --github-pr-comments: missing GITHUB_TOKEN, GITHUB_REPOSITORY, or GITHUB_SHA — skipping inline comments.");
            return;
        }

        if (prNumber is null)
        {
            Console.Error.WriteLine(
                "[GauntletCI] --github-pr-comments: cannot determine PR number " +
                "(set GAUNTLETCI_PR_NUMBER or ensure GITHUB_REF is refs/pull/*/merge) — skipping inline comments.");
            return;
        }

        var groups = FindingGrouper.Group(result.Findings);

        var inlineGroups = groups
            .Where(g => !string.IsNullOrEmpty(g.FilePath) && g.PrimaryLine.HasValue)
            .ToList();

        var summaryGroups = groups
            .Where(g => string.IsNullOrEmpty(g.FilePath) || !g.PrimaryLine.HasValue)
            .ToList();

        var url = $"https://api.github.com/repos/{repository}/pulls/{prNumber}/reviews";

        // First attempt: inline comments + summary body.
        // If GitHub rejects (422, line not in diff), fall back to summary-only.
        var retry = await TryPostReviewAsync(githubAuth, url, sha, inlineGroups, summaryGroups, ct);
        if (retry)
            await TryPostReviewAsync(githubAuth, url, sha, [], [.. summaryGroups, .. inlineGroups], ct);
    }

    /// <summary>
    /// Derives the PR number from GAUNTLETCI_PR_NUMBER (explicit override) or GITHUB_REF
    /// (format: <c>refs/pull/{number}/merge</c>).
    /// </summary>
    public static int? ResolvePrNumber()
    {
        var explicit_ = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        if (int.TryParse(explicit_, out var n) && n > 0)
            return n;

        var ghRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        if (!string.IsNullOrEmpty(ghRef))
        {
            // refs/pull/42/merge → ["refs", "pull", "42", "merge"]
            var parts = ghRef.Split('/');
            if (parts.Length >= 4 && parts[1] == "pull" && int.TryParse(parts[2], out var prN) && prN > 0)
                return prN;
        }

        return null;
    }

    /// <summary>
    /// Builds the markdown body for an inline diff comment on a specific finding.
    /// </summary>
    public static string BuildCommentBody(Finding finding)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{finding.RuleId} — {finding.RuleName}**");
        sb.AppendLine();
        sb.AppendLine(finding.Summary);

        if (!string.IsNullOrWhiteSpace(finding.Evidence))
        {
            sb.AppendLine();
            sb.AppendLine(FormatEvidenceMarkdown(finding.Evidence));
        }

        if (!string.IsNullOrWhiteSpace(finding.WhyItMatters))
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️ **Why it matters:** {finding.WhyItMatters}");
        }

        if (!string.IsNullOrWhiteSpace(finding.SuggestedAction))
        {
            sb.AppendLine();
            sb.AppendLine($"💡 **Suggested action:** {finding.SuggestedAction}");
        }

        if (!string.IsNullOrWhiteSpace(finding.LlmExplanation))
        {
            sb.AppendLine();
            sb.AppendLine($"🤖 **LLM insight:** {finding.LlmExplanation}");
        }

        if (finding.ExpertContext is { } ctx)
        {
            sb.AppendLine();
            sb.AppendLine($"📚 **Expert context:** {ctx.Content} _(source: {ctx.Source})_");
        }

        if (!string.IsNullOrWhiteSpace(finding.CoverageNote))
        {
            sb.AppendLine();
            sb.AppendLine($"📊 **Coverage:** {finding.CoverageNote}");
        }

        if (finding.TicketContext is not null)
        {
            var t = finding.TicketContext;
            var link = t.Url is not null ? $"[{t.Id}]({t.Url})" : t.Id;
            sb.AppendLine();
            sb.AppendLine($"🎫 **Ticket ({t.Provider}):** {link} — {t.Title}");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.AppendLine($"> {t.Description}");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append($"<sub>Confidence: {finding.Confidence} | Severity: {finding.Severity}</sub>");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the markdown body for a grouped inline diff comment. When the same rule fires
    /// against multiple lines in the same file, all hits are collapsed into one comment with a
    /// multi-line evidence list. Mirrors the run-log console layout: Summary / Evidence / Why /
    /// Action (+ optional LLM/Expert/Coverage/Ticket).
    /// </summary>
    public static string BuildCommentBody(GroupedFinding group)
    {
        var sb = new StringBuilder();
        var lineLabel = group.Lines.Count > 1
            ? $" — lines {string.Join(", ", group.Lines)}"
            : string.Empty;
        sb.AppendLine($"**{group.RuleId} — {group.RuleName}**{lineLabel}");
        sb.AppendLine();
        sb.AppendLine(group.Summary);

        if (group.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Evidence:**");
            foreach (var ev in group.Evidence)
                sb.AppendLine(FormatEvidenceMarkdown(ev));
        }

        if (!string.IsNullOrWhiteSpace(group.WhyItMatters))
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️ **Why it matters:** {group.WhyItMatters}");
        }

        if (!string.IsNullOrWhiteSpace(group.SuggestedAction))
        {
            sb.AppendLine();
            sb.AppendLine($"💡 **Suggested action:** {group.SuggestedAction}");
        }

        if (!string.IsNullOrWhiteSpace(group.LlmExplanation))
        {
            sb.AppendLine();
            sb.AppendLine($"🤖 **LLM insight:** {group.LlmExplanation}");
        }

        if (group.ExpertContext is { } ctx)
        {
            sb.AppendLine();
            sb.AppendLine($"📚 **Expert context:** {ctx.Content} _(source: {ctx.Source})_");
        }

        if (!string.IsNullOrWhiteSpace(group.CoverageNote))
        {
            sb.AppendLine();
            sb.AppendLine($"📊 **Coverage:** {group.CoverageNote}");
        }

        if (group.TicketContext is not null)
        {
            var t = group.TicketContext;
            var link = t.Url is not null ? $"[{t.Id}]({t.Url})" : t.Id;
            sb.AppendLine();
            sb.AppendLine($"🎫 **Ticket ({t.Provider}):** {link} — {t.Title}");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.AppendLine($"> {t.Description}");
        }

        sb.AppendLine();
        sb.Append($"<sub>Confidence: {group.Confidence} | Severity: {group.Severity}");
        if (group.Count > 1) sb.Append($" | {group.Count} occurrences");
        sb.Append("</sub>");

        return sb.ToString();
    }

    /// <summary>
    /// Formats an evidence string as GitHub-flavored Markdown.
    /// <list type="bullet">
    ///   <item><c>Was: X | Now: Y</c> → diff code block with a red removed line and a green added line.</item>
    ///   <item><c>Removed: X</c> → diff code block with a single red removed line.</item>
    ///   <item><c>Removed logic: A | B | C</c> → diff code block with one red line per item.</item>
    ///   <item>Anything else → plain blockquote.</item>
    /// </list>
    /// </summary>
    public static string FormatEvidenceMarkdown(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return string.Empty;

        // Was: X | Now: Y  →  diff block with - (red) and + (green)
        var wasNow = Regex.Match(evidence, @"^Was:\s*(.+?)\s*\|\s*Now:\s*(.+)$", RegexOptions.Singleline);
        if (wasNow.Success)
        {
            var was = wasNow.Groups[1].Value.Trim();
            var now = wasNow.Groups[2].Value.Trim();
            return $"```diff\n- {was}\n+ {now}\n```";
        }

        // Removed logic: A | B | C  →  diff block with one red line per item
        var removedLogic = Regex.Match(evidence, @"^Removed logic:\s*(.+)$", RegexOptions.Singleline);
        if (removedLogic.Success)
        {
            var items = removedLogic.Groups[1].Value.Split(" | ", StringSplitOptions.RemoveEmptyEntries);
            var lines = string.Join("\n", items.Select(i => $"- {i.Trim()}"));
            return $"```diff\n{lines}\n```";
        }

        // Removed: X  →  diff block with a single red line
        var removed = Regex.Match(evidence, @"^Removed:\s*(.+)$", RegexOptions.Singleline);
        if (removed.Success)
            return $"```diff\n- {removed.Groups[1].Value.Trim()}\n```";

        // Fallback: plain blockquote
        return $"> {evidence}";
    }

    // Returns true if the caller should retry without inline comments (422 from GitHub).
    private static async Task<bool> TryPostReviewAsync(
        string githubAuth,
        string url,
        string sha,
        List<GroupedFinding> inlineGroups,
        List<GroupedFinding> summaryGroups,
        CancellationToken ct)
    {
        var bodyText = BuildReviewBody(summaryGroups, hasInlineComments: inlineGroups.Count > 0);

        var payload = new ReviewPayload
        {
            CommitId = sha,
            Body     = bodyText,
            Event    = "COMMENT",
            Comments = [.. inlineGroups.Select(g => new ReviewComment
            {
                Path = g.FilePath!,
                Line = g.PrimaryLine!.Value,
                Side = "RIGHT",
                Body = BuildCommentBody(g),
            })],
        };

        var json    = JsonSerializer.Serialize(payload, _jsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubAuth);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GauntletCI", "2.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Content = content;

        try
        {
            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine(
                    $"[GauntletCI] --github-pr-comments: posted review with " +
                    $"{inlineGroups.Count} inline comment(s) and {summaryGroups.Count} summary entry(ies).");
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.Error.WriteLine(
                    "[GauntletCI] --github-pr-comments: 403 Forbidden — " +
                    "add `pull-requests: write` to your workflow permissions.");
                return false;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity && inlineGroups.Count > 0)
            {
                Console.Error.WriteLine(
                    "[GauntletCI] --github-pr-comments: one or more finding lines are outside the diff — " +
                    "retrying as summary comment.");
                return true;  // signal retry without inline comments
            }

            Console.Error.WriteLine(
                $"[GauntletCI] --github-pr-comments: API error {response.StatusCode} — {responseBody}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] --github-pr-comments: request failed — {ex.Message}");
            return false;
        }
    }

    public static string BuildReviewBody(List<GroupedFinding> summaryGroups, bool hasInlineComments)
    {
        if (summaryGroups.Count == 0)
            return hasInlineComments
                ? "**GauntletCI** found issues in this PR. See inline comments for details."
                : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("**GauntletCI** found the following issues:");
        sb.AppendLine();

        foreach (var g in summaryGroups)
        {
            var location = !string.IsNullOrEmpty(g.FilePath)
                ? (g.Lines.Count > 1
                    ? $" (`{g.FilePath}` lines {string.Join(", ", g.Lines)})"
                    : g.PrimaryLine.HasValue
                        ? $" (`{g.FilePath}:{g.PrimaryLine}`)"
                        : $" (`{g.FilePath}`)")
                : string.Empty;
            sb.AppendLine($"- **{g.RuleId} — {g.RuleName}**{location}: {g.Summary}");
        }

        if (hasInlineComments)
        {
            sb.AppendLine();
            sb.Append("Additional findings are posted as inline comments on the diff.");
        }

        return sb.ToString().TrimEnd();
    }

    private sealed class ReviewPayload
    {
        [JsonPropertyName("commit_id")]
        public required string CommitId { get; init; }

        [JsonPropertyName("body")]
        public required string Body { get; init; }

        [JsonPropertyName("event")]
        public required string Event { get; init; }

        [JsonPropertyName("comments")]
        public required List<ReviewComment> Comments { get; init; }
    }

    private sealed class ReviewComment
    {
        [JsonPropertyName("path")]
        public required string Path { get; init; }

        [JsonPropertyName("line")]
        public required int Line { get; init; }

        [JsonPropertyName("side")]
        public required string Side { get; init; }

        [JsonPropertyName("body")]
        public required string Body { get; init; }
    }
}
