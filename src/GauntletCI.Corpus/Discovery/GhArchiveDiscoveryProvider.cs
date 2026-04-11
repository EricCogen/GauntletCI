// SPDX-License-Identifier: Elastic-2.0
using System.IO.Compression;
using System.Text.Json;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Discovery;

public sealed class GhArchiveDiscoveryProvider : IDiscoveryProvider
{
    private static readonly HttpClient _http = new();

    public string GetProviderName() => "gh-archive";

    public bool SupportsIncrementalSync => false;

    public async Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken cancellationToken = default)
    {
        var archiveSlots = BuildArchiveSlots(query);
        var seen    = new HashSet<(string Owner, string Repo, int Number)>();
        var results = new List<PullRequestCandidate>();

        foreach (var (date, hour) in archiveSlots)
        {
            if (results.Count >= query.MaxCandidates)
                break;

            var url      = $"https://data.gharchive.org/{date:yyyy-MM-dd}-{hour}.json.gz";
            var dateSlot = $"{date:yyyy-MM-dd}-{hour}";

            byte[]? compressedData;
            try
            {
                compressedData = await _http.GetByteArrayAsync(url, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Archive slot may not exist yet (e.g., future hours) — skip silently
                continue;
            }

            using var memStream    = new MemoryStream(compressedData);
            using var gzipStream   = new GZipStream(memStream, CompressionMode.Decompress);
            using var reader       = new StreamReader(gzipStream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (results.Count >= query.MaxCandidates)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("type", out var typeEl))
                        continue;

                    if (typeEl.GetString() != "PullRequestEvent")
                        continue;

                    if (!root.TryGetProperty("payload", out var payload))
                        continue;

                    if (!payload.TryGetProperty("action", out var actionEl) ||
                        actionEl.GetString() != "closed")
                        continue;

                    if (!payload.TryGetProperty("pull_request", out var pr))
                        continue;

                    if (!pr.TryGetProperty("merged", out var mergedEl) || !mergedEl.GetBoolean())
                        continue;

                    var candidate = MapToCandidate(root, pr, query, dateSlot);
                    if (candidate is null)
                        continue;

                    var key = (candidate.RepoOwner, candidate.RepoName, candidate.PullRequestNumber);
                    if (!seen.Add(key))
                        continue;

                    results.Add(candidate);
                }
                catch (JsonException)
                {
                    // Malformed line — skip
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<(DateTime Date, int Hour)> BuildArchiveSlots(DiscoveryQuery query)
    {
        var start = query.StartDateUtc.HasValue
            ? query.StartDateUtc.Value.Date
            : DateTime.UtcNow.Date.AddDays(-1);

        var end = query.EndDateUtc.HasValue
            ? query.EndDateUtc.Value.Date
            : start;

        var slots = new List<(DateTime, int)>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            for (var h = 0; h < 24; h++)
                slots.Add((d, h));
        }

        return slots;
    }

    private static PullRequestCandidate? MapToCandidate(
        JsonElement root, JsonElement pr, DiscoveryQuery query, string dateSlot)
    {
        if (!root.TryGetProperty("repo", out var repoEl))
            return null;

        if (!repoEl.TryGetProperty("name", out var nameEl))
            return null;

        var repoFullName = nameEl.GetString() ?? "";
        var parts        = repoFullName.Split('/', 2);
        if (parts.Length < 2)
            return null;

        var owner = parts[0];
        var repo  = parts[1];

        var prNumber  = pr.TryGetProperty("number",   out var numEl)     ? numEl.GetInt32()     : 0;
        var htmlUrl   = pr.TryGetProperty("html_url", out var urlEl)     ? urlEl.GetString()    ?? "" : "";
        var createdAt = pr.TryGetProperty("created_at",out var createdEl) ? createdEl.GetDateTime() : DateTime.UtcNow;

        string language = "";
        if (pr.TryGetProperty("base", out var baseEl) &&
            baseEl.TryGetProperty("repo", out var baseRepoEl) &&
            baseRepoEl.TryGetProperty("language", out var langEl) &&
            langEl.ValueKind != JsonValueKind.Null)
        {
            language = langEl.GetString() ?? "";
        }

        if (query.Languages.Count > 0 &&
            !string.IsNullOrEmpty(language) &&
            !query.Languages.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)))
            return null;

        var fullRepo = $"{owner}/{repo}";

        if (query.RepoBlockList.Count > 0 &&
            query.RepoBlockList.Any(r => string.Equals(r, fullRepo, StringComparison.OrdinalIgnoreCase)))
            return null;

        return new PullRequestCandidate
        {
            Source             = "gh-archive",
            RepoOwner          = owner,
            RepoName           = repo,
            PullRequestNumber  = prNumber,
            Url                = htmlUrl,
            Language           = language,
            CreatedAtUtc       = createdAt,
            MergeState         = MergeState.Merged,
            CandidateReason    = $"gh-archive:{dateSlot}",
        };
    }
}
