// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Fetches the PR author's commit history for the specific repository to measure experience.
/// First-time or low-experience contributors have statistically higher defect rates
/// (Kamei et al. 2013 EXP/REXP features; Foucault et al. 2015 code ownership).
/// Results are written to the <c>author_experience_enrichments</c> table.
/// </summary>
public sealed class AuthorExperienceEnricher : IDisposable
{
    private const int CommitCountCap = 1000;

    private readonly HttpClient _http = HttpClientFactory.GetGitHubClient();
    // In-memory cache: repo -> set of contributor logins
    private readonly Dictionary<string, HashSet<string>> _contributorCache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsAuthenticated => _http.DefaultRequestHeaders.Contains("Authorization");

    public void Dispose()
    {
        // Factory manages the HttpClient lifetime, so we don't dispose it
    }

    public async Task<AuthorExperienceResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        int delayMs = 400,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            progress?.Invoke("[author-experience] WARNING: no GitHub token. Set GITHUB_TOKEN or run 'gh auth login'. Aborting.");
            return new AuthorExperienceResult(0, 0, 0, AuthMissing: true);
        }

        int processed = 0, firstContributors = 0, lowExperienceCount = 0;

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var parts = fixture.Repo.Split('/', 2);
            if (parts.Length < 2) continue;
            var owner = parts[0];
            var repo  = parts[1];

            // Step 1: get author login from PR API
            var authorLogin = await FetchAuthorLoginAsync(owner, repo, fixture.PullRequestNumber, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authorLogin)) continue;

            await Task.Delay(150, ct).ConfigureAwait(false);

            // Step 2: get commit count via pagination header
            var commitCount = await FetchCommitCountAsync(owner, repo, authorLogin, ct).ConfigureAwait(false);

            await Task.Delay(150, ct).ConfigureAwait(false);

            // Step 3: get contributor list (cached per repo)
            var contributors = await GetContributorsAsync(owner, repo, ct).ConfigureAwait(false);
            var isFirstContributor = !contributors.Contains(authorLogin);

            var tier = ClassifyExperienceTier(commitCount);

            await WriteDataAsync(
                db, fixture.FixtureId, fixture.Repo,
                authorLogin, commitCount, isFirstContributor, tier, ct).ConfigureAwait(false);

            processed++;
            if (isFirstContributor) firstContributors++;
            if (tier is "none" or "low") lowExperienceCount++;

            progress?.Invoke(
                $"[author-experience] {fixture.FixtureId}: {authorLogin} - " +
                $"commits={commitCount}, tier={tier}, first={isFirstContributor}");

            if (delayMs > 0)
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }

        return new AuthorExperienceResult(processed, firstContributors, lowExperienceCount, AuthMissing: false);
    }

    private async Task<string?> FetchAuthorLoginAsync(
        string owner, string repo, int prNumber, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.TryGetProperty("user", out var user) &&
                user.TryGetProperty("login", out var login))
                return login.GetString();
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private async Task<int> FetchCommitCountAsync(
        string owner, string repo, string login, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/commits?author={Uri.EscapeDataString(login)}&per_page=1";
        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return 0;

            // Try to parse total from Link header
            resp.Headers.TryGetValues("Link", out var linkValues);
            var linkHeader = linkValues?.FirstOrDefault();
            var lastPage = ParseLastPage(linkHeader);

            if (lastPage > 0)
                return Math.Min(lastPage, CommitCountCap);

            // No Link header - read array length (0 or 1)
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.GetArrayLength();
            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    private async Task<HashSet<string>> GetContributorsAsync(
        string owner, string repo, CancellationToken ct)
    {
        var key = $"{owner}/{repo}";
        if (_contributorCache.TryGetValue(key, out var cached))
            return cached;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var url = $"https://api.github.com/repos/{owner}/{repo}/contributors?per_page=100";
        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var contributor in doc.RootElement.EnumerateArray())
                    {
                        if (contributor.TryGetProperty("login", out var l))
                        {
                            var login = l.GetString();
                            if (!string.IsNullOrEmpty(login))
                                set.Add(login);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* contributors are best-effort */ }

        _contributorCache[key] = set;
        return set;
    }

    private static async Task WriteDataAsync(
        CorpusDb db, string fixtureId, string repo,
        string authorLogin, int commitCount, bool isFirstContributor,
        string experienceTier, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO author_experience_enrichments
                (fixture_id, repo, author_login, commit_count, is_first_contributor, experience_tier)
            VALUES
                ($fixtureId, $repo, $authorLogin, $commitCount, $isFirstContributor, $experienceTier)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",          fixtureId);
        cmd.Parameters.AddWithValue("$repo",               repo);
        cmd.Parameters.AddWithValue("$authorLogin",        authorLogin);
        cmd.Parameters.AddWithValue("$commitCount",        commitCount);
        cmd.Parameters.AddWithValue("$isFirstContributor", isFirstContributor ? 1 : 0);
        cmd.Parameters.AddWithValue("$experienceTier",     experienceTier);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // ── internal static helpers (tested directly) ─────────────────────────────

    internal static string ClassifyExperienceTier(int commitCount) => commitCount switch
    {
        0    => "none",
        <= 5 => "low",
        <= 50 => "medium",
        _    => "high",
    };

    /// <summary>
    /// Parses the GitHub Link header to find the last page number.
    /// Format: &lt;https://api.github.com/...?page=N&gt;; rel="last"
    /// Returns 0 if no last page is found.
    /// </summary>
    internal static int ParseLastPage(string? linkHeader)
    {
        if (string.IsNullOrEmpty(linkHeader)) return 0;
        foreach (var part in linkHeader.Split(','))
        {
            var trimmed = part.Trim();
            if (!trimmed.Contains("rel=\"last\"")) continue;
            var urlPart = trimmed.Split(';')[0].Trim().Trim('<', '>');
            var query = new Uri(urlPart).Query;
            foreach (var param in query.TrimStart('?').Split('&'))
            {
                var kv = param.Split('=');
                if (kv.Length == 2 && kv[0] == "page" && int.TryParse(kv[1], out var n))
                    return n;
            }
        }
        return 0;
    }
}

/// <summary>Summary statistics from a <see cref="AuthorExperienceEnricher.EnrichAsync"/> run.</summary>
public record AuthorExperienceResult(
    int  FixturesProcessed,
    int  FirstContributors,
    int  LowExperienceCount,
    bool AuthMissing);
