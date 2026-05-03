// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// HTTP client for the SonarCloud public API (no authentication required for public projects).
/// </summary>
public sealed class SonarCloudClient
{
    private const string BaseUrl = "https://sonarcloud.io/api";

    private readonly HttpClient _http = HttpClientFactory.GetSonarCloudClient();

    /// <summary>
    /// Attempts to find the SonarCloud project key for a given GitHub owner/repo.
    /// First tries the conventional key format (<c>owner_repo</c> lowercased), then
    /// falls back to an org-scoped component search.
    /// Returns <c>null</c> if no matching project is found.
    /// </summary>
    public async Task<string?> FindProjectKeyAsync(string owner, string repo, CancellationToken ct = default)
    {
        var conventional = $"{owner.ToLowerInvariant()}_{repo.ToLowerInvariant()}";
        if (await ProjectExistsAsync(conventional, ct))
            return conventional;

        // Org search fallback: some projects use non-conventional keys
        var url = $"{BaseUrl}/components/search"
                + $"?organization={Uri.EscapeDataString(owner.ToLowerInvariant())}"
                + $"&q={Uri.EscapeDataString(repo)}"
                + "&qualifiers=TRK&ps=50";

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("components", out var components))
                return null;

            foreach (var c in components.EnumerateArray())
            {
                if (!c.TryGetProperty("name", out var nameEl)) continue;
                if (!string.Equals(nameEl.GetString(), repo, StringComparison.OrdinalIgnoreCase)) continue;
                return c.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* project not on SonarCloud */ }

        return null;
    }

    /// <summary>
    /// Fetches all open BUG and VULNERABILITY issues for a SonarCloud project.
    /// Paginates transparently (SonarCloud max 500 items per page).
    /// Adds a 1-second courtesy delay between pages to avoid hammering the public API.
    /// </summary>
    public async Task<IReadOnlyList<SonarIssue>> GetIssuesAsync(string projectKey, CancellationToken ct = default)
    {
        var results = new List<SonarIssue>();
        int page = 1;

        while (true)
        {
            var url = $"{BaseUrl}/issues/search"
                    + $"?componentKeys={Uri.EscapeDataString(projectKey)}"
                    + "&statuses=OPEN,CONFIRMED"
                    + "&types=BUG,VULNERABILITY"
                    + $"&ps=500&p={page}";

            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) break;

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                if (!doc.RootElement.TryGetProperty("issues", out var issues)) break;

                int countThisPage = 0;
                foreach (var issue in issues.EnumerateArray())
                {
                    var si = MapIssue(issue, projectKey);
                    if (si is not null)
                    {
                        results.Add(si);
                        countThisPage++;
                    }
                }

                if (countThisPage == 0) break;

                // SonarCloud paging: stop when we have all items
                if (doc.RootElement.TryGetProperty("paging", out var paging))
                {
                    var total    = paging.TryGetProperty("total",    out var t)  ? t.GetInt32()  : 0;
                    var pageSize = paging.TryGetProperty("pageSize", out var ps) ? ps.GetInt32() : 500;
                    if (results.Count >= total || pageSize == 0) break;
                }
                else
                {
                    break;
                }

                page++;
                await Task.Delay(1_000, ct); // courtesy throttle for public API
            }
            catch (OperationCanceledException) { throw; }
            catch { break; }
        }

        return results;
    }

    private async Task<bool> ProjectExistsAsync(string projectKey, CancellationToken ct)
    {
        var url = $"{BaseUrl}/components/show?component={Uri.EscapeDataString(projectKey)}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static SonarIssue? MapIssue(JsonElement issue, string projectKey)
    {
        if (!issue.TryGetProperty("component", out var compEl)) return null;
        var component = compEl.GetString() ?? "";

        // SonarCloud component paths: "project_key:src/path/to/File.cs"
        // Strip the "project_key:" prefix to get the repo-relative path.
        var prefix = projectKey + ":";
        var filePath = component.StartsWith(prefix, StringComparison.Ordinal)
            ? component[prefix.Length..]
            : component;

        return new SonarIssue
        {
            ProjectKey = projectKey,
            FilePath   = filePath,
            Rule       = issue.TryGetProperty("rule",     out var r)  ? r.GetString()  ?? "" : "",
            Severity   = issue.TryGetProperty("severity", out var s)  ? s.GetString()  ?? "" : "",
            Type       = issue.TryGetProperty("type",     out var tp) ? tp.GetString() ?? "" : "",
            Message    = issue.TryGetProperty("message",  out var m)  ? m.GetString()  ?? "" : "",
        };
    }
}
