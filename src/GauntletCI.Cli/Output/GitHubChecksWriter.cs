// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
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
                "[GauntletCI] --github-checks: missing GITHUB_TOKEN, GITHUB_REPOSITORY, or GITHUB_SHA — skipping.");
            return;
        }

        var conclusion  = BuildConclusion(result);
        var annotations = BuildAnnotations(result);

        var blockCount = result.Findings.Count(f => f.Severity == RuleSeverity.Block);
        var warnCount  = result.Findings.Count(f => f.Severity == RuleSeverity.Warn);

        var payload = new
        {
            name       = "GauntletCI Risk Analysis",
            head_sha   = sha,
            status     = "completed",
            conclusion,
            output     = new
            {
                title       = $"{blockCount} Block findings, {warnCount} Warn findings",
                summary     = $"GauntletCI found {blockCount} high-risk changes.",
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
                Console.Error.WriteLine($"[GauntletCI] --github-checks: API error {response.StatusCode} — {body}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] --github-checks: request failed — {ex.Message}");
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
    /// Block findings are prioritized first, then Warn, Info, Advisory.
    /// Capped at 50; only findings with both FilePath and Line are included.
    /// </summary>
    internal static List<object> BuildAnnotations(EvaluationResult result)
    {
        return result.Findings
            .Where(f => !string.IsNullOrEmpty(f.FilePath) && f.Line.HasValue)
            .OrderBy(f => SeverityPriority(f.Severity))
            .Take(50)
            .Select(f => (object)new
            {
                path             = f.FilePath!,
                start_line       = f.Line!.Value,
                end_line         = f.Line!.Value,
                annotation_level = ToAnnotationLevel(f.Severity),
                title            = $"{f.RuleId} — {f.RuleName}",
                message          = f.Summary,
                raw_details      = $"{f.WhyItMatters}\n\n{f.SuggestedAction}",
            })
            .ToList();
    }

    // Block=0 (highest priority), Warn=1, Info=2, Advisory=3 — enum values cannot be relied on.
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
