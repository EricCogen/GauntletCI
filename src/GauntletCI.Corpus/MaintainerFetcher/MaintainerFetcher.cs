// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core;

namespace GauntletCI.Corpus.MaintainerFetcher;

/// <summary>
/// Fetches high-signal PRs and issues from target OSS repos, filtered to top contributors.
/// Set GITHUB_TOKEN env var for authenticated requests (5000 req/hr vs 60 unauthenticated).
/// </summary>
public sealed class MaintainerFetcher : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private const double TopPercentile = 0.05; // top 5%
    private const int MinTopCount = 10;         // always take at least 10

    /// <summary>
    /// Initializes the fetcher with an externally owned or injected HTTP client.
    /// </summary>
    /// <param name="http">Pre-configured HTTP client (auth headers should already be set).</param>
    /// <param name="ownsHttpClient">When true, disposes <paramref name="http"/> on <see cref="Dispose"/>.</param>
    public MaintainerFetcher(HttpClient http, bool ownsHttpClient = false)
    {
        _http = http;
        _ownsHttpClient = ownsHttpClient;
    }

    /// <summary>
    /// Creates a fully configured fetcher using the GITHUB_TOKEN environment variable for auth.
    /// The returned instance owns its HTTP client and will dispose it on <see cref="Dispose"/>.
    /// </summary>
    public static MaintainerFetcher CreateDefault()
    {
        var http = HttpClientFactory.GetGitHubClient();
        return new MaintainerFetcher(http, ownsHttpClient: false);
    }

    /// <summary>Disposes the HTTP client when this instance owns it.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient) _http.Dispose();
    }

    /// <summary>
    /// For each target repo: identifies top-5% contributors by commit count, then fetches
    /// merged PRs and open/closed issues filtered by the target labels. Returns deduplicated
    /// MaintainerRecord list for LLM distillation.
    /// </summary>
    /// <param name="targets">Target repos and labels to fetch; defaults to <see cref="MaintainerTarget.Defaults"/>.</param>
    /// <param name="maxPerLabel">Maximum items to fetch per label per repo.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<MaintainerRecord>> FetchAsync(
        MaintainerTarget[]? targets = null,
        int maxPerLabel = 100,
        CancellationToken ct = default)
    {
        targets ??= MaintainerTarget.Defaults;
        var results = new Dictionary<string, MaintainerRecord>(); // key = "{owner}/{repo}#{number}:{type}"

        foreach (var target in targets)
        {
            var topLogins = await GetTopContributorLoginsAsync(target.Owner, target.Repo, ct).ConfigureAwait(false);
            if (topLogins.Count == 0) continue;

            foreach (var label in target.Labels)
            {
                var prs    = await SearchItemsAsync(target.Owner, target.Repo, "pr",    label, topLogins, maxPerLabel, ct).ConfigureAwait(false);
                var issues = await SearchItemsAsync(target.Owner, target.Repo, "issue", label, topLogins, maxPerLabel, ct).ConfigureAwait(false);

                foreach (var rec in prs.Concat(issues))
                {
                    var key = $"{rec.Owner}/{rec.Repo}#{rec.Number}:{rec.Type}";
                    results.TryAdd(key, rec);
                }
            }
        }

        return [.. results.Values.OrderByDescending(r => r.Reactions)];
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<string>> GetTopContributorLoginsAsync(
        string owner, string repo, CancellationToken ct)
    {
        // GitHub returns contributors sorted by contributions desc (first page = highest)
        var url  = $"https://api.github.com/repos/{owner}/{repo}/contributors?per_page=100&anon=0";
        var json = await FetchWithBackoffAsync(url, ct).ConfigureAwait(false);
        var contributors = JsonSerializer.Deserialize<List<GhContributor>>(json, JsonOpts) ?? [];

        var total   = contributors.Count;
        // Top-5% threshold: at minimum 10 contributors, ceil to avoid fractional counts
        var takeN   = Math.Max(MinTopCount, (int)Math.Ceiling(total * TopPercentile));
        return contributors.Take(takeN).Select(c => c.Login).ToList();
    }

    internal async Task<List<MaintainerRecord>> SearchItemsAsync(
        string owner, string repo, string type, string label,
        IReadOnlyList<string> topLogins, int max, CancellationToken ct)
    {
        var loginSet = new HashSet<string>(topLogins, StringComparer.OrdinalIgnoreCase);

        // URL-encode the label (handles "area-System.Runtime" etc.)
        var encodedLabel = Uri.EscapeDataString(label);
        var qualifier = type == "pr" ? "is:pr+is:merged" : "is:issue";
        var url = $"https://api.github.com/search/issues" +
                  $"?q=repo:{owner}/{repo}+{qualifier}+label:{encodedLabel}" +
                  $"&sort=reactions&order=desc&per_page={Math.Min(max, 100)}";

        var json     = await FetchWithBackoffAsync(url, ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<GhSearchResponse>(json, JsonOpts);
        if (response?.Items is null) return [];

        var records = new List<MaintainerRecord>();
        foreach (var item in response.Items)
        {
            if (!loginSet.Contains(item.User.Login)) continue;
            var itemType = item.PullRequest is not null ? "pr" : "issue";
            records.Add(new MaintainerRecord
            {
                Owner     = owner,
                Repo      = repo,
                Number    = item.Number,
                Type      = itemType,
                Author    = item.User.Login,
                Title     = item.Title,
                Body      = item.Body ?? "",
                Labels    = item.Labels.Select(l => l.Name).ToArray(),
                Url       = item.HtmlUrl,
                CreatedAt = item.CreatedAt,
                Reactions = item.Reactions?.TotalCount ?? 0,
            });
        }
        return records;
    }

    private async Task<string> FetchWithBackoffAsync(string url, CancellationToken ct)
    {
        const int MaxRetries = 6;
        var baseDelay = TimeSpan.FromSeconds(2);

        for (int attempt = 0; ; attempt++)
        {
            using var req  = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!IsRateLimited(resp) || attempt >= MaxRetries)
                resp.EnsureSuccessStatusCode();

            var wait = GetWaitTime(resp, baseDelay);
            baseDelay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * 2, 64));
            Console.Error.WriteLine($"[maintainer-fetcher] Rate limited (HTTP {(int)resp.StatusCode}): waiting {wait.TotalSeconds:F0}s…");
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }
    }

    private static bool IsRateLimited(HttpResponseMessage resp) =>
        CorpusStringHelpers.IsRateLimited(resp);

    private static TimeSpan GetWaitTime(HttpResponseMessage resp, TimeSpan fallback)
    {
        if (resp.Headers.RetryAfter?.Delta is { } delta) return delta + TimeSpan.FromSeconds(1);
        if (resp.Headers.TryGetValues("x-ratelimit-reset", out var resetVals) &&
            long.TryParse(resetVals.FirstOrDefault(), out var epoch))
        {
            var wait = DateTimeOffset.FromUnixTimeSeconds(epoch) - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            if (wait > TimeSpan.Zero) return wait;
        }
        var jitter = 1.0 + (Random.Shared.NextDouble() * 0.2 - 0.1);
        return TimeSpan.FromSeconds(fallback.TotalSeconds * jitter);
    }

    // ── JSON models ──────────────────────────────────────────────────────────

    private sealed class GhContributor
    {
        [JsonPropertyName("login")]        public string Login         { get; init; } = "";
        [JsonPropertyName("contributions")] public int Contributions   { get; init; }
    }

    private sealed class GhSearchResponse
    {
        [JsonPropertyName("items")] public List<GhSearchItem>? Items { get; init; }
    }

    private sealed class GhSearchItem
    {
        [JsonPropertyName("number")]       public int Number           { get; init; }
        [JsonPropertyName("title")]        public string Title         { get; init; } = "";
        [JsonPropertyName("body")]         public string? Body         { get; init; }
        [JsonPropertyName("html_url")]     public string HtmlUrl       { get; init; } = "";
        [JsonPropertyName("created_at")]   public DateTimeOffset CreatedAt { get; init; }
        [JsonPropertyName("user")]         public GhUser User          { get; init; } = new();
        [JsonPropertyName("labels")]       public List<GhLabel> Labels { get; init; } = [];
        [JsonPropertyName("reactions")]    public GhReactions? Reactions { get; init; }
        [JsonPropertyName("pull_request")] public GhPrRef? PullRequest { get; init; }
    }

    private sealed class GhUser    { [JsonPropertyName("login")] public string Login { get; init; } = ""; }
    private sealed class GhLabel   { [JsonPropertyName("name")]  public string Name  { get; init; } = ""; }
    private sealed class GhReactions { [JsonPropertyName("total_count")] public int TotalCount { get; init; } }
    private sealed class GhPrRef   { [JsonPropertyName("url")]   public string Url   { get; init; } = ""; }
}
