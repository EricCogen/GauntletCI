// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Tier 2 oracle: enriches each fixture with PR review metadata (time to merge, reviewer count,
/// comment count) and computes a <see cref="SocialSignalScore"/> (0.0 = low-validation, 1.0 = well-reviewed).
/// Low-validation PRs - merged in under 10 minutes with zero human reviewers - are the
/// HIGH_RISK_GHOST and UNVALIDATED_BEHAVIORAL_RISK training targets from the Ground Truth
/// Implementation Guide.
/// Results are written to the <c>social_signal_enrichments</c> table.
/// </summary>
public sealed class SocialSignalEnricher : IDisposable
{
    private readonly HttpClient _http = HttpClientFactory.GetGitHubClient();

    public bool IsAuthenticated =>
        _http.DefaultRequestHeaders.Contains("Authorization");

    public void Dispose()
    {
        // Factory manages the HttpClient lifetime, so we don't dispose it
    }

    /// <summary>
    /// Fetches PR metadata and reviews for each fixture, computes a social signal score,
    /// and writes the results to the <c>social_signal_enrichments</c> table.
    /// </summary>
    public async Task<SocialSignalEnrichmentResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        int delayMs = 300,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[social-signal] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new SocialSignalEnrichmentResult { AuthMissing = true };
        }

        var result = new SocialSignalEnrichmentResult();

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            var signals = await FetchSignalsAsync(parts[0], parts[1], fixture.PullRequestNumber, ct).ConfigureAwait(false);
            if (signals is null) continue;

            await WriteSignalsAsync(db, fixture.FixtureId, fixture.Repo, fixture.PullRequestNumber, signals, ct).ConfigureAwait(false);
            result.FixturesProcessed++;

            if (signals.SocialSignalScore < 0.3)
            {
                result.LowValidationFixtures++;
                progress?.Invoke(
                    $"[social-signal] {fixture.FixtureId}: LOW_VALIDATION " +
                    $"(score={signals.SocialSignalScore:F2}, " +
                    $"time={signals.ReviewTimeMinutes:F0}min, " +
                    $"reviewers={signals.ReviewerCount})");
            }

            if (delayMs > 0)
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<SocialSignalData?> FetchSignalsAsync(
        string owner, string repo, int prNumber, CancellationToken ct)
    {
        DateTime? createdAt = null, mergedAt = null;
        int requestedReviewers = 0;

        var prUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
        try
        {
            using var prResp = await _http.GetAsync(prUrl, ct).ConfigureAwait(false);
            if (!prResp.IsSuccessStatusCode) return null;

            await using var stream = await prResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("created_at", out var ca) && ca.ValueKind != JsonValueKind.Null)
                createdAt = ca.GetDateTime();

            if (root.TryGetProperty("merged_at", out var ma) && ma.ValueKind != JsonValueKind.Null)
                mergedAt = ma.GetDateTime();

            if (root.TryGetProperty("requested_reviewers", out var rrEl) &&
                rrEl.ValueKind == JsonValueKind.Array)
                requestedReviewers = rrEl.GetArrayLength();
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }

        await Task.Delay(150, ct).ConfigureAwait(false);

        var reviewsUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/reviews?per_page=100";
        int reviewCount = 0, botReviewCount = 0;
        var reviewerLogins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var revResp = await _http.GetAsync(reviewsUrl, ct).ConfigureAwait(false);
            if (revResp.IsSuccessStatusCode)
            {
                await using var stream = await revResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var review in doc.RootElement.EnumerateArray())
                    {
                        reviewCount++;
                        if (review.TryGetProperty("user", out var u) &&
                            u.TryGetProperty("login", out var l))
                        {
                            var login = l.GetString() ?? "";
                            reviewerLogins.Add(login);
                            if (login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase))
                                botReviewCount++;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* reviews are best-effort */ }

        var humanReviewers  = Math.Max(0, reviewerLogins.Count - botReviewCount);
        var isBotMerged     = humanReviewers == 0 && botReviewCount > 0;
        var reviewTimeMin   = (createdAt.HasValue && mergedAt.HasValue)
            ? (mergedAt.Value - createdAt.Value).TotalMinutes
            : -1.0;

        // Score computation (0.0 = low validation, 1.0 = high validation)
        // Starts at 0.5 and adjusts based on three factors: time, reviewers, review count.
        double score = 0.5;

        if (reviewTimeMin >= 0)
        {
            if      (reviewTimeMin <  10) score -= 0.30;
            else if (reviewTimeMin >= 60) score += 0.15;
        }

        if      (humanReviewers == 0) score -= 0.25;
        else if (humanReviewers == 1) score += 0.10;
        else if (humanReviewers >= 2) score += 0.20;

        if      (reviewCount == 0)   score -= 0.10;
        else if (reviewCount >= 3)   score += 0.10;

        if (isBotMerged)             score -= 0.15;

        score = Math.Clamp(score, 0.0, 1.0);

        return new SocialSignalData
        {
            ReviewTimeMinutes  = reviewTimeMin,
            ReviewerCount      = humanReviewers,
            ReviewCommentCount = reviewCount,
            IsBotMerged        = isBotMerged,
            SocialSignalScore  = score,
        };
    }

    private static async Task WriteSignalsAsync(
        CorpusDb db, string fixtureId, string repo, int prNumber,
        SocialSignalData signals, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO social_signal_enrichments
                (fixture_id, repo, pr_number,
                 review_time_minutes, reviewer_count, review_comment_count,
                 is_bot_merged, social_signal_score)
            VALUES
                ($fixtureId, $repo, $prNumber,
                 $reviewTime, $reviewerCount, $commentCount,
                 $isBotMerged, $score)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",    fixtureId);
        cmd.Parameters.AddWithValue("$repo",          repo);
        cmd.Parameters.AddWithValue("$prNumber",      prNumber);
        cmd.Parameters.AddWithValue("$reviewTime",    signals.ReviewTimeMinutes);
        cmd.Parameters.AddWithValue("$reviewerCount", signals.ReviewerCount);
        cmd.Parameters.AddWithValue("$commentCount",  signals.ReviewCommentCount);
        cmd.Parameters.AddWithValue("$isBotMerged",   signals.IsBotMerged ? 1 : 0);
        cmd.Parameters.AddWithValue("$score",         signals.SocialSignalScore);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private sealed record SocialSignalData
    {
        public double ReviewTimeMinutes  { get; init; }
        public int    ReviewerCount      { get; init; }
        public int    ReviewCommentCount { get; init; }
        public bool   IsBotMerged        { get; init; }
        public double SocialSignalScore  { get; init; }
    }
}

/// <summary>Summary statistics from a <see cref="SocialSignalEnricher.EnrichAsync"/> run.</summary>
public sealed class SocialSignalEnrichmentResult
{
    public bool AuthMissing           { get; set; }
    public int  FixturesProcessed     { get; set; }
    public int  LowValidationFixtures { get; set; }
}
