// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Creates a GitHub Checks API check run with annotated findings.
/// Requires GITHUB_TOKEN, GITHUB_REPOSITORY, and GITHUB_SHA env vars.
/// The workflow must declare <c>checks: write</c> permission.
/// Soft-fails on missing env vars or API errors.
/// </summary>
public static class GitHubChecksWriter
{
    private static readonly HttpClient _http = HttpClientFactory.GetGitHubClient();
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    /// <summary>Posts findings as a GitHub Checks API check run. Soft-fails on any error.</summary>
    public static async Task WriteAsync(EvaluationResult result, CancellationToken ct = default)
    {
        var token      = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var sha        = Environment.GetEnvironmentVariable("GITHUB_SHA");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(sha))
        {
            Console.Error.WriteLine(
                "[GauntletCI] --github-checks: missing GITHUB_TOKEN, GITHUB_REPOSITORY, or GITHUB_SHA: skipping.");
            return;
        }

        var conclusion  = BuildConclusion(result);
        var annotations = BuildAnnotations(result);

        var blockCount = result.Findings.Count(f => f.Severity == RuleSeverity.Block);
        var warnCount  = result.Findings.Count(f => f.Severity == RuleSeverity.Warn);
        var groupCount = FindingGrouper.Group(result.Findings).Count;

        var titleText = result.Findings.Count == 0
            ? "No risks detected"
            : $"{groupCount} grouped finding{(groupCount == 1 ? "" : "s")} ({blockCount} block, {warnCount} warn)";

        var payload = new
        {
            name       = "GauntletCI Risk Analysis",
            head_sha   = sha,
            status     = "completed",
            conclusion,
            output     = new
            {
                title       = titleText,
                summary     = BuildSummaryMarkdown(result),
                annotations,
            }
        };

        var json    = JsonSerializer.Serialize(payload, _jsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url     = $"https://api.github.com/repos/{repository}/check-runs";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GauntletCI", "2.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Content = content;

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                Console.Error.WriteLine($"[GauntletCI] --github-checks: API error {response.StatusCode}: {body}");
            }
            else
            {
                Console.Error.WriteLine(
                    $"[GauntletCI] --github-checks: posted check run with {groupCount} finding(s), conclusion={conclusion}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] --github-checks: request failed: {ex.Message}");
        }
    }

    /// <summary>Derives the check run conclusion from the evaluation result.</summary>
    internal static string BuildConclusion(EvaluationResult result)
    {
        if (result.Findings.Any(f => f.Severity == RuleSeverity.Block))
            return "failure";
        if (result.Findings.Any(f => f.Severity is RuleSeverity.Warn or RuleSeverity.Info))
            return "neutral";
        return "success";
    }

    /// <summary>
    /// Builds the annotation list for the check run output.
    /// Findings are grouped by (RuleId, FilePath); one annotation is emitted per group.
    /// Block groups are prioritized first, then Warn, Info, Advisory.
    /// Capped at 50; only groups with both FilePath and a primary line are included.
    /// </summary>
    internal static List<object> BuildAnnotations(EvaluationResult result)
    {
        return FindingGrouper.Group(result.Findings)
            .Where(g => !string.IsNullOrEmpty(g.FilePath) && g.PrimaryLine.HasValue)
            .OrderBy(g => SeverityPriority(g.Severity))
            .Take(50)
            .Select(g =>
            {
                var lineLabel = g.Lines.Count > 1
                    ? $" (lines {string.Join(", ", g.Lines)})"
                    : string.Empty;
                var rawDetails = BuildRawDetails(g);
                return (object)new
                {
                    path             = g.FilePath!,
                    start_line       = g.PrimaryLine!.Value,
                    end_line         = g.PrimaryLine!.Value,
                    annotation_level = ToAnnotationLevel(g.Severity),
                    title            = $"{g.RuleId}: {g.RuleName}{lineLabel}",
                    message          = g.Summary,
                    raw_details      = rawDetails,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds the multi-section markdown body shown in the check run summary panel.
    /// Grouped by severity (Block / Warn / Info), each entry mirrors the console run-log layout.
    /// </summary>
    internal static string BuildSummaryMarkdown(EvaluationResult result)
    {
        if (result.Findings.Count == 0)
            return "✅ **GauntletCI**: Scan complete. No risk signals detected.";

        var groups = FindingGrouper.Group(result.Findings);
        var sb = new StringBuilder();
        sb.AppendLine("### GauntletCI Risk Analysis");
        sb.AppendLine();
        sb.AppendLine($"**{groups.Count}** grouped finding(s) across **{result.RulesEvaluated}** rules evaluated.");
        sb.AppendLine();

        var sections = new[]
        {
            (RuleSeverity.Block, "🚫 Possible Block"),
            (RuleSeverity.Warn,  "⚠️ Warn"),
            (RuleSeverity.Info,  "ℹ️ Info"),
            (RuleSeverity.Advisory, "💡 Advisory"),
        };

        foreach (var (severity, label) in sections)
        {
            var section = groups.Where(g => g.Severity == severity).ToList();
            if (section.Count == 0) continue;

            sb.AppendLine($"#### {label} ({section.Count})");
            sb.AppendLine();
            foreach (var g in section)
            {
                sb.AppendLine(RenderGroupMarkdown(g));
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildRawDetails(GroupedFinding g)
    {
        var sb = new StringBuilder();
        if (g.Evidence.Count > 0)
        {
            sb.AppendLine("Evidence:");
            foreach (var ev in g.Evidence)
                sb.AppendLine($"  - {ev}");
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(g.WhyItMatters))
        {
            sb.AppendLine($"Why: {g.WhyItMatters}");
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(g.SuggestedAction))
            sb.Append($"Action: {g.SuggestedAction}");
        return sb.ToString().TrimEnd();
    }

    private static string RenderGroupMarkdown(GroupedFinding g)
    {
        var sb = new StringBuilder();
        var loc = g.FilePath is not null
            ? (g.Lines.Count > 1
                ? $": `{g.FilePath}` (lines {string.Join(", ", g.Lines)})"
                : g.PrimaryLine.HasValue
                    ? $": `{g.FilePath}:{g.PrimaryLine}`"
                    : $": `{g.FilePath}`")
            : string.Empty;

        sb.AppendLine($"**{g.RuleId}: {g.RuleName}**{loc}");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {g.Summary}");

        if (g.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Evidence:**");
            foreach (var ev in g.Evidence)
                sb.AppendLine($"- {ev}");
        }

        if (!string.IsNullOrWhiteSpace(g.WhyItMatters))
        {
            sb.AppendLine();
            sb.AppendLine($"**Why it matters:** {g.WhyItMatters}");
        }

        if (!string.IsNullOrWhiteSpace(g.SuggestedAction))
        {
            sb.AppendLine();
            sb.AppendLine($"**Suggested action:** {g.SuggestedAction}");
        }

        sb.AppendLine();
        sb.Append($"<sub>Confidence: {g.Confidence} · Severity: {g.Severity}");
        if (g.Count > 1) sb.Append($" · {g.Count} occurrences");
        sb.Append("</sub>");

        return sb.ToString();
    }

    // Block=0 (highest priority), Warn=1, Info=2, Advisory=3: enum values cannot be relied on.
    private static int SeverityPriority(RuleSeverity s) => s switch
    {
        RuleSeverity.Block    => 0,
        RuleSeverity.Warn     => 1,
        RuleSeverity.Info     => 2,
        _                     => 3,   // Advisory, None
    };

    private static string ToAnnotationLevel(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Block => "failure",
        RuleSeverity.Warn  => "warning",
        _                  => "notice",
    };
}
