// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Normalization;

/// <summary>
/// Orchestrates the full normalization flow:
/// HydratedPullRequest → all fixture files on disk + SQLite index entry.
///
/// Writes: metadata.json, expected.json, diff.patch, notes.md (via store),
/// and updates the SQLite fixtures table. Idempotent: safe to re-run.
/// </summary>
public sealed class NormalizationPipeline
{
    private readonly FixtureFolderStore _store;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public NormalizationPipeline(FixtureFolderStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Normalizes a hydrated PR into its full fixture folder structure.
    /// Returns the written <see cref="FixtureMetadata"/>.
    /// </summary>
    public async Task<FixtureMetadata> NormalizeAsync(
        HydratedPullRequest pr,
        string source = "manual",
        FixtureTier tier = FixtureTier.Discovery,
        CancellationToken ct = default)
    {
        var metadata = FixtureNormalizer.Normalize(pr, source, tier);

        // 1. metadata.json + notes.md template + SQLite upsert
        await _store.SaveMetadataAsync(metadata, ct);

        // 2. diff.patch
        await WriteDiffPatchAsync(metadata, pr.DiffText, ct);

        // 3. expected.json: empty list for discovery-tier fixtures
        //    (human or heuristic labels will populate this later)
        if (metadata.Tier == FixtureTier.Discovery)
            await _store.SaveExpectedFindingsAsync(metadata.FixtureId, [], ct);

        return metadata;
    }

    /// <summary>
    /// Re-normalizes a fixture that already has raw/ snapshots on disk,
    /// without hitting the GitHub API again. Preserves the original hydration
    /// timestamp from existing metadata.json to keep normalization idempotent.
    /// </summary>
    public async Task<FixtureMetadata> ReNormalizeFromRawAsync(
        string fixtureId,
        FixtureTier tier,
        string repoOwner,
        string repoName,
        int prNumber,
        CancellationToken ct = default)
    {
        var fixturePath = FixtureIdHelper.GetFixturePath(_store.BasePath, tier, fixtureId);
        var rawPath     = FixtureIdHelper.GetRawPath(fixturePath);

        var prJsonPath       = Path.Combine(rawPath, "pr.json");
        var filesJsonPath    = Path.Combine(rawPath, "files.json");
        var commentsJsonPath = Path.Combine(rawPath, "review-comments.json");
        var diffPatchPath    = Path.Combine(fixturePath, "diff.patch");

        if (!File.Exists(prJsonPath))
            throw new FileNotFoundException($"Raw snapshot not found: {prJsonPath}");
        if (!File.Exists(filesJsonPath))
            throw new FileNotFoundException($"Raw snapshot not found: {filesJsonPath}");
        if (!File.Exists(commentsJsonPath))
            throw new FileNotFoundException($"Raw snapshot not found: {commentsJsonPath}");

        var prJson       = await File.ReadAllTextAsync(prJsonPath, ct);
        var filesJson    = await File.ReadAllTextAsync(filesJsonPath, ct);
        var commentsJson = await File.ReadAllTextAsync(commentsJsonPath, ct);
        var diffText     = File.Exists(diffPatchPath)
            ? await File.ReadAllTextAsync(diffPatchPath, ct) : "";

        // Preserve original hydration timestamp for idempotency
        var originalTimestamp = await ReadExistingHydratedAtUtcAsync(fixturePath, ct);

        var pr = ReconstructFromRaw(
            repoOwner, repoName, prNumber,
            prJson, filesJson, commentsJson, diffText,
            originalTimestamp);

        return await NormalizeAsync(pr, source: "re-normalized", tier: tier, ct: ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task WriteDiffPatchAsync(FixtureMetadata meta, string diffText, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(diffText)) return;

        var fixturePath  = FixtureIdHelper.GetFixturePath(_store.BasePath, meta.Tier, meta.FixtureId);
        var diffPatchPath = Path.Combine(fixturePath, "diff.patch");
        await File.WriteAllTextAsync(diffPatchPath, diffText, ct);
    }

    private static async Task<DateTime?> ReadExistingHydratedAtUtcAsync(
        string fixturePath, CancellationToken ct)
    {
        var metaPath = Path.Combine(fixturePath, "metadata.json");
        if (!File.Exists(metaPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(metaPath, ct);
            var meta = JsonSerializer.Deserialize<FixtureMetadata>(json, JsonOpts);
            return meta?.CreatedAtUtc;
        }
        catch
        {
            return null;
        }
    }

    private static HydratedPullRequest ReconstructFromRaw(
        string owner, string repo, int prNumber,
        string prJson, string filesJson, string commentsJson, string diffText,
        DateTime? preservedTimestamp)
    {
        var ghPr       = JsonSerializer.Deserialize<RawPrSnapshot>(prJson, JsonOpts);
        var ghFiles    = JsonSerializer.Deserialize<List<RawFileSnapshot>>(filesJson, JsonOpts) ?? [];
        var ghComments = JsonSerializer.Deserialize<List<RawCommentSnapshot>>(commentsJson, JsonOpts) ?? [];

        var changedFiles = ghFiles.Select(f => new ChangedFile
        {
            Path        = f.Filename ?? "",
            Status      = f.Status ?? "",
            Additions   = f.Additions,
            Deletions   = f.Deletions,
            Patch       = f.Patch ?? "",
            IsTestFile  = TestFileClassifier.IsTestFile(f.Filename ?? ""),
            LanguageHint = GuessLanguage(f.Filename ?? ""),
        }).ToList();

        var reviewComments = ghComments.Select(c => new ReviewComment
        {
            Author       = c.User?.Login ?? "",
            Body         = c.Body ?? "",
            Path         = c.Path ?? "",
            DiffHunk     = c.DiffHunk ?? "",
            Position     = c.Position ?? 0,
            CreatedAtUtc = c.CreatedAt,
            Url          = c.HtmlUrl ?? "",
        }).ToList();

        var hydratedAt = preservedTimestamp ?? DateTime.UtcNow;

        return new HydratedPullRequest
        {
            RepoOwner         = owner,
            RepoName          = repo,
            PullRequestNumber = prNumber,
            Title             = ghPr?.Title ?? "",
            Body              = ghPr?.Body ?? "",
            BaseSha           = ghPr?.Base?.Sha ?? "",
            HeadSha           = ghPr?.Head?.Sha ?? "",
            MergeCommitSha    = ghPr?.MergeCommitSha ?? "",
            FilesChangedCount = ghPr?.ChangedFiles ?? changedFiles.Count,
            Additions         = ghPr?.Additions ?? 0,
            Deletions         = ghPr?.Deletions ?? 0,
            ChangedFiles      = changedFiles,
            ReviewComments    = reviewComments,
            DiffText          = diffText,
            HydratedAtUtc     = hydratedAt,
        };
    }

    private static string GuessLanguage(string path) =>
        CorpusStringHelpers.GuessLanguage(path);

    // Minimal raw snapshot shapes for re-normalization deserialization
    private sealed class RawPrSnapshot
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]        public string? Title { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("body")]         public string? Body { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("additions")]    public int Additions { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("deletions")]    public int Deletions { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("changed_files")] public int ChangedFiles { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("merge_commit_sha")] public string? MergeCommitSha { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("base")]         public RawRef? Base { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("head")]         public RawRef? Head { get; init; }
    }

    private sealed class RawRef
    {
        [System.Text.Json.Serialization.JsonPropertyName("sha")] public string? Sha { get; init; }
    }

    private sealed class RawFileSnapshot
    {
        [System.Text.Json.Serialization.JsonPropertyName("filename")]  public string? Filename { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("status")]    public string? Status { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("additions")] public int Additions { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("deletions")] public int Deletions { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("patch")]     public string? Patch { get; init; }
    }

    private sealed class RawCommentSnapshot
    {
        [System.Text.Json.Serialization.JsonPropertyName("user")]       public RawUser? User { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("body")]       public string? Body { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("path")]       public string? Path { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("diff_hunk")]  public string? DiffHunk { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("position")]   public int? Position { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("html_url")]   public string? HtmlUrl { get; init; }
    }

    private sealed class RawUser
    {
        [System.Text.Json.Serialization.JsonPropertyName("login")] public string? Login { get; init; }
    }
}
