// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Core.Model;

namespace GauntletCI.Cli.IncidentCorrelation;

/// <summary>Represents a normalised incident or alert from PagerDuty or Opsgenie.</summary>
public record IncidentSummary(string Id, string Title, string? Description, string Source);

/// <summary>
/// Fetches incidents from PagerDuty and Opsgenie, and correlates them with GauntletCI findings.
/// All HTTP calls soft-fail: network errors log to stderr and return empty lists.
/// </summary>
public static class IncidentClient
{
    private static readonly HttpClient _http = HttpClientFactory.GetGenericClient();

    // ── Duration parsing ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses a duration string like "24h", "7d", "30m" and returns the point in time
    /// that far before <paramref name="now"/>. Falls back to 24 hours on invalid input.
    /// </summary>
    internal static DateTimeOffset ParseSince(string since, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(since) || since.Length < 2)
            return now.AddHours(-24);

        var unit = char.ToLowerInvariant(since[^1]);
        if (!int.TryParse(since[..^1], out var value) || value <= 0)
            return now.AddHours(-24);

        return unit switch
        {
            'h' => now.AddHours(-value),
            'd' => now.AddDays(-value),
            'm' => now.AddMinutes(-value),
            'w' => now.AddDays(-value * 7),
            _   => now.AddHours(-24),
        };
    }

    // ── PagerDuty ─────────────────────────────────────────────────────────────

    /// <summary>Fetches PagerDuty incidents in the given time window.</summary>
    public static async Task<List<IncidentSummary>> FetchPagerDutyAsync(
        string token,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken ct = default)
    {
        try
        {
            var sinceStr = since.UtcDateTime.ToString("o");
            var untilStr = until.UtcDateTime.ToString("o");
            var url = $"https://api.pagerduty.com/incidents?since={Uri.EscapeDataString(sinceStr)}&until={Uri.EscapeDataString(untilStr)}&statuses[]=triggered&statuses[]=acknowledged&statuses[]=resolved&limit=100";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", $"token={token}");
            request.Headers.Accept.ParseAdd("application/vnd.pagerduty+json;version=2");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[GauntletCI] PagerDuty fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return [];
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var incidents = new List<IncidentSummary>();
            if (doc.RootElement.TryGetProperty("incidents", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id    = item.TryGetProperty("id",    out var p) ? p.GetString() ?? "" : "";
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    string? desc = null;
                    if (item.TryGetProperty("description", out var d))
                        desc = d.GetString();
                    incidents.Add(new IncidentSummary(id, title, desc, "PagerDuty"));
                }
            }

            return incidents;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[GauntletCI] PagerDuty fetch error: {ex.Message}");
            return [];
        }
    }

    /// <summary>Posts a note to a PagerDuty incident. Returns true if the post succeeded.</summary>
    public static async Task<bool> PostPagerDutyNoteAsync(
        string token,
        string incidentId,
        string content,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.pagerduty.com/incidents/{Uri.EscapeDataString(incidentId)}/notes";
            var body = JsonSerializer.Serialize(new { note = new { content } });

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", $"token={token}");
            request.Headers.Accept.ParseAdd("application/vnd.pagerduty+json;version=2");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                Console.Error.WriteLine($"[GauntletCI] PagerDuty note post failed: {(int)response.StatusCode}: {err}");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[GauntletCI] PagerDuty note error: {ex.Message}");
            return false;
        }
    }

    // ── Opsgenie ─────────────────────────────────────────────────────────────

    /// <summary>Fetches Opsgenie alerts within the given time window.</summary>
    public static async Task<List<IncidentSummary>> FetchOpsgenieAsync(
        string token,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken ct = default)
    {
        try
        {
            // Opsgenie query language: createdAt > {epoch_ms} AND createdAt < {epoch_ms}
            var sinceMs = since.ToUnixTimeMilliseconds();
            var untilMs = until.ToUnixTimeMilliseconds();
            var query = Uri.EscapeDataString($"createdAt > {sinceMs} AND createdAt < {untilMs}");
            var url = $"https://api.opsgenie.com/v2/alerts?query={query}&limit=100&order=desc";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("GenieKey", token);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[GauntletCI] Opsgenie fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return [];
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var alerts = new List<IncidentSummary>();
            if (doc.RootElement.TryGetProperty("data", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id      = item.TryGetProperty("id",      out var p) ? p.GetString() ?? "" : "";
                    var message = item.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                    string? desc = null;
                    if (item.TryGetProperty("description", out var d))
                        desc = d.GetString();
                    alerts.Add(new IncidentSummary(id, message, desc, "Opsgenie"));
                }
            }

            return alerts;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[GauntletCI] Opsgenie fetch error: {ex.Message}");
            return [];
        }
    }

    // ── Correlation ───────────────────────────────────────────────────────────

    /// <summary>
    /// For each finding that has a file path, checks whether any incident's title or
    /// description contains the file name (simple substring match). Returns a map from
    /// file path to matching incidents.
    /// </summary>
    internal static Dictionary<string, List<IncidentSummary>> CorrelateIncidents(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<IncidentSummary> incidents)
    {
        var result = new Dictionary<string, List<IncidentSummary>>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.FilePath))
                continue;

            var fileName = Path.GetFileName(finding.FilePath);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            if (result.ContainsKey(finding.FilePath))
                continue;

            var matches = incidents.Where(inc =>
                Contains(inc.Title, fileName) ||
                Contains(inc.Title, finding.FilePath) ||
                Contains(inc.Description, fileName) ||
                Contains(inc.Description, finding.FilePath))
                .ToList();

            result[finding.FilePath] = matches;
        }

        return result;
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null &&
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    // ── Heatmap builder ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds the JSON representation of the change-risk heatmap.
    /// </summary>
    internal static string BuildHeatmapJson(
        string baseRef,
        DateTimeOffset since,
        DateTimeOffset until,
        IReadOnlyList<Finding> findings,
        Dictionary<string, List<IncidentSummary>> correlations,
        IReadOnlyList<IncidentSummary> allIncidents)
    {
        // Group findings by file path
        var byFile = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.FilePath))
            .GroupBy(f => f.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var filePath   = g.Key;
                var maxSev     = g.Max(f => f.Severity);
                var correlated = correlations.TryGetValue(filePath, out var incs) ? incs : [];
                return new
                {
                    file = filePath,
                    maxSeverity = maxSev.ToString(),
                    findings = g.Select(f => new
                    {
                        f.RuleId,
                        f.RuleName,
                        f.Summary,
                        severity = f.Severity.ToString(),
                        confidence = f.Confidence.ToString(),
                        f.Line,
                    }).ToList(),
                    correlatedIncidents = correlated.Select(i => new
                    {
                        i.Id,
                        i.Title,
                        i.Description,
                        i.Source,
                    }).ToList(),
                };
            })
            .ToList();

        var obj = new
        {
            baseRef,
            since = since.UtcDateTime.ToString("o"),
            until = until.UtcDateTime.ToString("o"),
            files = byFile,
            allIncidents = allIncidents.Select(i => new { i.Id, i.Title, i.Description, i.Source }).ToList(),
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }
}
