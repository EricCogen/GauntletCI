// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Annotates Block-severity findings with Codecov coverage data.
/// Soft-fails when env vars are missing or API calls fail.
/// </summary>
public static class CoverageCorrelator
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Fetches Codecov commit coverage and annotates Block findings whose files have zero coverage.
    /// Requires CODECOV_TOKEN, GITHUB_REPOSITORY, and GITHUB_SHA env vars.
    /// </summary>
    public static async Task AnnotateAsync(EvaluationResult result, CancellationToken ct = default)
    {
        var codecovToken = Environment.GetEnvironmentVariable("CODECOV_TOKEN");
        var githubRepo   = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var githubSha    = Environment.GetEnvironmentVariable("GITHUB_SHA");

        if (string.IsNullOrEmpty(codecovToken)
            || string.IsNullOrEmpty(githubRepo)
            || string.IsNullOrEmpty(githubSha))
            return;

        var repoParts = githubRepo.Split('/');
        if (repoParts.Length < 2)
            return;

        var owner = repoParts[0];
        var repo  = repoParts[1];

        try
        {
            var url = $"https://api.codecov.io/api/v2/gh/{owner}/repos/{repo}/commits/{githubSha}/";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", codecovToken);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GauntletCI", "2.0"));

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(ct);
            var fileCoverageMap = ParseCoverageResponse(json);

            // Skip annotation entirely when the coverage map could not be parsed —
            // we must not flag files as zero-coverage based on missing data.
            if (fileCoverageMap is null)
                return;

            var blockFindings = result.Findings
                .Where(f => f.Severity == RuleSeverity.Block && !string.IsNullOrEmpty(f.FilePath))
                .ToList();

            foreach (var finding in blockFindings)
            {
                if (fileCoverageMap.TryGetValue(finding.FilePath!, out var cov) && cov == 0.0)
                    finding.CoverageNote = "⚠️ No test coverage detected for this file (Codecov).";
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] Coverage correlation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the Codecov commit JSON response and returns a map of file name → coverage percentage.
    /// Returns null when the response contains no per-file data.
    /// </summary>
    internal static Dictionary<string, double>? ParseCoverageResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;
            var result     = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            if (!root.TryGetProperty("files", out var filesEl)
                || filesEl.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var file in filesEl.EnumerateArray())
            {
                if (!file.TryGetProperty("name", out var nameEl))
                    continue;

                var name   = nameEl.GetString() ?? string.Empty;
                double cov = 0.0;

                if (file.TryGetProperty("totals", out var totals)
                    && totals.TryGetProperty("coverage", out var covEl)
                    && covEl.ValueKind == JsonValueKind.Number)
                {
                    cov = covEl.GetDouble();
                }

                result[name] = cov;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }
}
