// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Core;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Fetches the PR title and body from the GitHub REST API and computes description quality signals.
/// Empty or very short PR descriptions correlate with rushed, poorly-considered changes
/// (Zanoni et al. 2018).
/// Results are written to the <c>pr_description_enrichments</c> table.
/// </summary>
public sealed class PRDescriptionEnricher : IDisposable
{
    private static readonly Regex LinkedIssuePattern = new(
        @"(?i)(fixes|closes|resolves)\s+#\d+|(?i)(fixes|closes|resolves)\s+https://github\.com/[^/]+/[^/]+/issues/\d+",
        RegexOptions.Compiled);

    private static readonly string[] WipKeywords =
    [
        "wip", "work in progress", "draft", "do not merge", "dnm", "temp", "hack", "fixme", "todo",
    ];

    private readonly HttpClient _http = HttpClientFactory.GetGitHubClient();

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public void Dispose()
    {
        // Factory manages the HttpClient lifetime, so we don't dispose it
    }

    public async Task<PRDescriptionResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        int delayMs = 250,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[pr-description] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new PRDescriptionResult(0, 0, 0, AuthMissing: true);
        }

        int processed = 0, emptyBodyCount = 0, linkedIssueCount = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;

            var data = await FetchPrDataAsync(parts[0], parts[1], fixture.PullRequestNumber, ct);
            if (data is null) continue;

            await WriteDataAsync(db, fixture.FixtureId, fixture.Repo, data, ct);
            processed++;

            if (data.IsEmptyBody)    emptyBodyCount++;
            if (data.HasLinkedIssue) linkedIssueCount++;

            progress?.Invoke(
                $"[pr-description] {fixture.FixtureId}: " +
                $"body={data.BodyLength}ch, linked={data.HasLinkedIssue}, wip={data.HasWipKeywords}");

            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
        }

        return new PRDescriptionResult(processed, emptyBodyCount, linkedIssueCount, AuthMissing: false);
    }

    private async Task<PrDescriptionData?> FetchPrDataAsync(
        string owner, string repo, int prNumber, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind != JsonValueKind.Null
                ? titleEl.GetString() ?? "" : "";

            string? body = null;
            if (root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind != JsonValueKind.Null)
                body = bodyEl.GetString();

            int labelCount = 0;
            if (root.TryGetProperty("labels", out var labelsEl) && labelsEl.ValueKind == JsonValueKind.Array)
                labelCount = labelsEl.GetArrayLength();

            return new PrDescriptionData
            {
                TitleLength    = title.Length,
                BodyLength     = body?.Length ?? 0,
                IsEmptyBody    = IsBodyEmpty(body),
                HasLinkedIssue = HasLinkedIssue(body),
                HasWipKeywords = HasWipKeywords(title, body),
                LabelCount     = labelCount,
            };
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static async Task WriteDataAsync(
        CorpusDb db, string fixtureId, string repo, PrDescriptionData data, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO pr_description_enrichments
                (fixture_id, repo, title_length, body_length, is_empty_body,
                 has_linked_issue, has_wip_keywords, label_count)
            VALUES
                ($fixtureId, $repo, $titleLength, $bodyLength, $isEmptyBody,
                 $hasLinkedIssue, $hasWipKeywords, $labelCount)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",      fixtureId);
        cmd.Parameters.AddWithValue("$repo",           repo);
        cmd.Parameters.AddWithValue("$titleLength",    data.TitleLength);
        cmd.Parameters.AddWithValue("$bodyLength",     data.BodyLength);
        cmd.Parameters.AddWithValue("$isEmptyBody",    data.IsEmptyBody    ? 1 : 0);
        cmd.Parameters.AddWithValue("$hasLinkedIssue", data.HasLinkedIssue ? 1 : 0);
        cmd.Parameters.AddWithValue("$hasWipKeywords", data.HasWipKeywords ? 1 : 0);
        cmd.Parameters.AddWithValue("$labelCount",     data.LabelCount);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── internal static helpers (tested directly) ─────────────────────────────

    internal static bool IsBodyEmpty(string? body) =>
        body is null || body.Trim().Length < 30;

    internal static bool HasLinkedIssue(string? body) =>
        body is not null && LinkedIssuePattern.IsMatch(body);

    internal static bool HasWipKeywords(string title, string? body)
    {
        var combined = (title + " " + (body ?? "")).ToLowerInvariant();
        foreach (var kw in WipKeywords)
            if (combined.Contains(kw, StringComparison.Ordinal))
                return true;
        return false;
    }

    private sealed record PrDescriptionData
    {
        public int  TitleLength    { get; init; }
        public int  BodyLength     { get; init; }
        public bool IsEmptyBody    { get; init; }
        public bool HasLinkedIssue { get; init; }
        public bool HasWipKeywords { get; init; }
        public int  LabelCount     { get; init; }
    }
}

/// <summary>Summary statistics from a <see cref="PRDescriptionEnricher.EnrichAsync"/> run.</summary>
public record PRDescriptionResult(
    int  FixturesProcessed,
    int  EmptyBodyCount,
    int  LinkedIssueCount,
    bool AuthMissing);
