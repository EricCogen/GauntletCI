// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Enricher that fetches PR review comments and applies a keyword taxonomy to extract
/// rule-specific intent signals. Results are written to <c>review_comment_nlp_enrichments</c>.
/// </summary>
public sealed class ReviewCommentNlpEnricher : IDisposable
{
    private static readonly IReadOnlyList<(string[] Keywords, string RuleId, double Confidence)> Taxonomy =
    [
        (["race condition", "thread safe", "concurrent access", "not thread-safe", "lock contention"], "GCI0016", 0.70),
        (["deadlock", "async over sync", ".result", ".wait()", "blocking call", "configureawait"],      "GCI0016", 0.70),
        (["null reference", "nullreferenceexception", "can this be null", "null check missing"],        "GCI0043", 0.65),
        (["memory leak", "not disposed", "should be disposed", "idisposable", "using statement"],       "GCI0024", 0.65),
        (["security", "injection", "xss", "csrf", "authentication bypass", "authorization"],            "GCI0012", 0.70),
        (["breaking change", "backwards compat", "backward compat", "api break", "semver"],             "GCI0004", 0.65),
        (["hardcoded", "hard-coded", "magic number", "magic string", "should use config"],              "GCI0010", 0.60),
        (["unhandled exception", "exception propagation", "missing catch", "uncaught"],                 "GCI0032", 0.65),
        (["performance", "hot path", "n+1", "allocation", "boxing", "linq in loop"],                   "GCI0044", 0.60),
        (["pii", "personal data", "gdpr", "sensitive data", "user data in log"],                       "GCI0029", 0.70),
        (["missing test", "needs test", "add a test", "no test coverage", "untested"],                  "GCI0041", 0.65),
        (["behavioral change", "side effect", "unexpected behavior", "regression"],                     "GCI0003", 0.60),
        (["floating point", "float equality", "double equality", "use epsilon"],                        "GCI0049", 0.65),
        (["schema change", "migration", "db schema", "database schema", "column removed"],              "GCI0021", 0.70),
    ];

    private readonly HttpClient _http;

    public ReviewCommentNlpEnricher()
    {
        var token = GitHubTokenResolver.Resolve();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    public void Dispose() => _http.Dispose();

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public async Task<NlpEnrichmentResult> EnrichAsync(
        IReadOnlyList<FixtureMetadata> fixtures,
        CorpusDb db,
        int delayMs,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[review-nlp] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new NlpEnrichmentResult(0, 0, 0, AuthMissing: true);
        }

        int processed = 0, fixturesWithMatches = 0, totalMatches = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            var combinedText = await FetchReviewTextAsync(
                parts[0], parts[1], fixture.PullRequestNumber, ct);

            if (delayMs > 0) await Task.Delay(delayMs, ct);

            var matches = MatchTaxonomy(combinedText);

            foreach (var (ruleId, keyword, confidence) in matches)
                await WriteMatchAsync(db, fixture.FixtureId, fixture.Repo, ruleId, keyword, confidence, ct);

            processed++;
            if (matches.Count > 0)
            {
                fixturesWithMatches++;
                totalMatches += matches.Count;
                progress?.Invoke(
                    $"[review-nlp] {fixture.FixtureId}: {matches.Count} match(es) " +
                    $"({string.Join(", ", matches.Select(m => m.RuleId).Distinct())})");
            }
        }

        return new NlpEnrichmentResult(processed, fixturesWithMatches, totalMatches, AuthMissing: false);
    }

    // Public static so tests and SilverLabelEngine can call it directly
    public static IReadOnlyList<(string RuleId, string MatchedKeyword, double Confidence)> MatchTaxonomy(
        string reviewText)
    {
        if (string.IsNullOrWhiteSpace(reviewText)) return [];

        var lower = reviewText.ToLowerInvariant();
        var results = new Dictionary<string, (string RuleId, string MatchedKeyword, double Confidence)>(
            StringComparer.Ordinal);

        foreach (var (keywords, ruleId, confidence) in Taxonomy)
        {
            foreach (var keyword in keywords)
            {
                if (lower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    // One match per rule ID - use the first matching keyword
                    if (!results.ContainsKey(ruleId))
                        results[ruleId] = (ruleId, keyword, confidence);
                    break;
                }
            }
        }

        return [.. results.Values];
    }

    // Allows SilverLabelEngine to query NLP matches from the DB
    public static async Task<IReadOnlyList<(string RuleId, string MatchedKeyword, double Confidence)>>
        QueryMatchesAsync(CorpusDb db, string fixtureId, CancellationToken ct = default)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT matched_rule_id, matched_keyword, confidence
            FROM   review_comment_nlp_enrichments
            WHERE  fixture_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", fixtureId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<(string, string, double)>();
        while (await reader.ReadAsync(ct))
            list.Add((reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        return list;
    }

    private async Task<string> FetchReviewTextAsync(
        string owner, string repo, int prNumber, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();

        // Inline review comments
        var commentsUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/comments?per_page=100";
        try
        {
            using var resp = await _http.GetAsync(commentsUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var comment in doc.RootElement.EnumerateArray())
                    {
                        if (comment.TryGetProperty("body", out var body) &&
                            body.ValueKind != JsonValueKind.Null)
                            sb.Append(' ').Append(body.GetString());
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* best effort */ }

        await Task.Delay(150, ct);

        // Review bodies
        var reviewsUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/reviews?per_page=100";
        try
        {
            using var resp = await _http.GetAsync(reviewsUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var review in doc.RootElement.EnumerateArray())
                    {
                        if (review.TryGetProperty("body", out var body) &&
                            body.ValueKind != JsonValueKind.Null)
                            sb.Append(' ').Append(body.GetString());
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* best effort */ }

        return sb.ToString();
    }

    private static async Task WriteMatchAsync(
        CorpusDb db, string fixtureId, string repo,
        string ruleId, string keyword, double confidence, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO review_comment_nlp_enrichments
                (fixture_id, repo, matched_rule_id, matched_keyword, confidence, fetched_at_utc)
            VALUES
                ($fixtureId, $repo, $ruleId, $keyword, $confidence, datetime('now'))
            """;
        cmd.Parameters.AddWithValue("$fixtureId",  fixtureId);
        cmd.Parameters.AddWithValue("$repo",        repo);
        cmd.Parameters.AddWithValue("$ruleId",      ruleId);
        cmd.Parameters.AddWithValue("$keyword",     keyword);
        cmd.Parameters.AddWithValue("$confidence",  confidence);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public record NlpEnrichmentResult(int FixturesProcessed, int FixturesWithMatches, int TotalMatches, bool AuthMissing);
