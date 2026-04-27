// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Enricher that parses NuGet package names from diff hunks (.csproj and packages.lock.json)
/// and queries the GitHub Advisory Database (GHSA) GraphQL API for known vulnerabilities.
/// Results are written to the <c>nuget_advisory_enrichments</c> table.
/// </summary>
public sealed class NuGetAdvisoryEnricher : IDisposable
{
    // Regex: PackageReference Include="PackageName" in .csproj added lines
    private static readonly Regex CsprojPackageRegex =
        new(@"<PackageReference\s+Include=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex: "PackageName" : { in packages.lock.json added lines
    private static readonly Regex LockFilePackageRegex =
        new(@"""([A-Za-z][A-Za-z0-9._-]+)""\s*:\s*\{", RegexOptions.Compiled);

    private readonly HttpClient _http;

    public NuGetAdvisoryEnricher()
    {
        var token = GitHubTokenResolver.Resolve();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v4+json"));
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    public void Dispose() => _http.Dispose();

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public async Task<NuGetAdvisoryResult> EnrichAsync(
        IReadOnlyList<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        int delayMs,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[nuget-advisory] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new NuGetAdvisoryResult(0, 0, 0, AuthMissing: true);
        }

        int processed = 0, fixturesWithAdvisories = 0, totalAdvisories = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var diffPath = Path.Combine(
                fixturesBasePath,
                fixture.Tier.ToString().ToLowerInvariant(),
                fixture.FixtureId,
                "diff.patch");

            if (!File.Exists(diffPath))
            {
                await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo, 0, 0, null, "[]", ct);
                continue;
            }

            var packages = ExtractPackageNames(diffPath);

            if (packages.Count == 0)
            {
                await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo, 0, 0, null, "[]", ct);
                processed++;
                continue;
            }

            var advisories = new List<object>();
            string? highestSeverity = null;

            foreach (var pkg in packages)
            {
                ct.ThrowIfCancellationRequested();
                var nodes = await QueryAdvisoriesAsync(pkg, ct);
                advisories.AddRange(nodes);
                if (delayMs > 0) await Task.Delay(delayMs, ct);
            }

            // Determine highest severity
            var severityOrder = new[] { "CRITICAL", "HIGH", "MODERATE", "LOW" };
            foreach (var sev in severityOrder)
            {
                if (advisories.Any(a => GetSeverity(a) == sev))
                {
                    highestSeverity = sev;
                    break;
                }
            }

            var advisoriesJson = JsonSerializer.Serialize(advisories);
            await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo,
                packages.Count, advisories.Count, highestSeverity, advisoriesJson, ct);

            processed++;
            if (advisories.Count > 0)
            {
                fixturesWithAdvisories++;
                totalAdvisories += advisories.Count;
                progress?.Invoke(
                    $"[nuget-advisory] {fixture.FixtureId}: {advisories.Count} advisory/advisories " +
                    $"(highest={highestSeverity ?? "none"}, packages={packages.Count})");
            }
        }

        return new NuGetAdvisoryResult(processed, fixturesWithAdvisories, totalAdvisories, AuthMissing: false);
    }

    // Public static so tests can call it directly
    public static IReadOnlyList<string> ExtractPackageNames(string diffPath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inCsproj    = false;
        var inLockFile  = false;

        foreach (var line in File.ReadLines(diffPath))
        {
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                var path = line[6..].ToLowerInvariant();
                inCsproj   = path.EndsWith(".csproj", StringComparison.Ordinal);
                inLockFile = path.EndsWith("packages.lock.json", StringComparison.Ordinal);
                continue;
            }

            if (!line.StartsWith("+", StringComparison.Ordinal) || line.StartsWith("+++", StringComparison.Ordinal))
                continue;

            var added = line[1..];

            if (inCsproj)
            {
                var m = CsprojPackageRegex.Match(added);
                if (m.Success) names.Add(m.Groups[1].Value);
            }
            else if (inLockFile)
            {
                var m = LockFilePackageRegex.Match(added);
                if (m.Success) names.Add(m.Groups[1].Value);
            }
        }

        return names.ToList();
    }

    private async Task<List<object>> QueryAdvisoriesAsync(string packageName, CancellationToken ct)
    {
        const string query = """
            query($pkg: String!) {
              securityVulnerabilities(ecosystem: NUGET, package: $pkg, first: 10) {
                nodes {
                  advisory { ghsaId severity publishedAt }
                  vulnerableVersionRange
                  firstPatchedVersion { identifier }
                }
              }
            }
            """;

        var payload = JsonSerializer.Serialize(new { query, variables = new { pkg = packageName } });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var resp = await _http.PostAsync("https://api.github.com/graphql", content, ct);
            if (!resp.IsSuccessStatusCode) return [];

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("data", out var data)) return [];
            if (!data.TryGetProperty("securityVulnerabilities", out var sv)) return [];
            if (!sv.TryGetProperty("nodes", out var nodes)) return [];
            if (nodes.ValueKind != JsonValueKind.Array) return [];

            var list = new List<object>();
            foreach (var node in nodes.EnumerateArray())
                list.Add(JsonSerializer.Deserialize<object>(node.GetRawText())!);
            return list;
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    private static string? GetSeverity(object advisory)
    {
        try
        {
            var json = JsonSerializer.Serialize(advisory);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("advisory", out var adv) &&
                adv.TryGetProperty("severity", out var sev))
                return sev.GetString()?.ToUpperInvariant();
        }
        catch { }
        return null;
    }

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        int packagesChecked, int advisoryCount, string? highestSeverity,
        string advisoriesJson, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO nuget_advisory_enrichments
                (fixture_id, repo, packages_checked, advisory_count,
                 highest_severity, advisories_json, scanned_at_utc)
            VALUES
                ($fixtureId, $repo, $packagesChecked, $advisoryCount,
                 $highestSeverity, $advisoriesJson, datetime('now'))
            """;
        cmd.Parameters.AddWithValue("$fixtureId",       fixtureId);
        cmd.Parameters.AddWithValue("$repo",             repo);
        cmd.Parameters.AddWithValue("$packagesChecked",  packagesChecked);
        cmd.Parameters.AddWithValue("$advisoryCount",    advisoryCount);
        cmd.Parameters.AddWithValue("$highestSeverity",  (object?)highestSeverity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$advisoriesJson",   advisoriesJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public record NuGetAdvisoryResult(int FixturesProcessed, int FixturesWithAdvisories, int TotalAdvisories, bool AuthMissing);
