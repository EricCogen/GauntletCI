// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Storage;

/// <summary>
/// Concrete <see cref="IFixtureStore"/> backed by the file system (JSON files) and
/// a SQLite index (<see cref="CorpusDb"/>).
/// </summary>
public sealed class FixtureFolderStore : IFixtureStore
{
    private readonly string _basePath;
    private readonly CorpusDb _db;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public FixtureFolderStore(CorpusDb db, string basePath = "./data/fixtures")
    {
        _db = db;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    /// <summary>
    /// Resolves the on-disk fixture directory using the SQLite path, configured fixtures root, and tier.
    /// </summary>
    public string? ResolveFixtureDirectory(string fixtureId, FixtureTier tier, string? storedPath = null)
    {
        foreach (var candidate in EnumerateFixtureDirectoryCandidates(
                     new FixtureIndexRow(fixtureId, tier.ToString(), storedPath, string.Empty, 0, string.Empty,
                         null, null, null, false, false, string.Empty, string.Empty)))
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }


    // ── IFixtureStore ────────────────────────────────────────────────────────

    public async Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken ct = default)
    {
        var fixturePath = EnsureFixtureDir(metadata.Tier, metadata.FixtureId);
        var metaPath = Path.Combine(fixturePath, "metadata.json");

        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(metadata, JsonOpts), ct).ConfigureAwait(false);
        EnsureNotesTemplate(fixturePath, metadata);
        await UpsertFixtureSqliteAsync(metadata, fixturePath, ct).ConfigureAwait(false);
    }

    public async Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken ct = default)
    {
        // Try each tier in preference order: gold > silver > discovery
        foreach (var tier in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
        {
            var path = Path.Combine(FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId), "metadata.json");
            if (!File.Exists(path)) continue;

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<FixtureMetadata>(json, JsonOpts);
        }
        return null;
    }

    public async Task SaveExpectedFindingsAsync(
        string fixtureId, IReadOnlyList<ExpectedFinding> findings, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId)
            ?? throw new InvalidOperationException($"Fixture '{fixtureId}' not found. Call SaveMetadataAsync first.");

        var expectedPath = Path.Combine(fixturePath, "expected.json");
        await File.WriteAllTextAsync(expectedPath, JsonSerializer.Serialize(findings, JsonOpts), ct).ConfigureAwait(false);
    }

    public async Task SaveActualFindingsAsync(
        string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId)
            ?? throw new InvalidOperationException($"Fixture '{fixtureId}' not found. Call SaveMetadataAsync first.");

        // Actual findings are stored per run so prior runs aren't overwritten.
        var actualPath = Path.Combine(fixturePath, $"actual.{runId}.json");
        await File.WriteAllTextAsync(actualPath, JsonSerializer.Serialize(findings, JsonOpts), ct).ConfigureAwait(false);

        // Also write/overwrite the canonical actual.json with the latest run.
        var latestPath = Path.Combine(fixturePath, "actual.json");
        await File.WriteAllTextAsync(latestPath, JsonSerializer.Serialize(findings, JsonOpts), ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(
        FixtureTier? tier = null, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();

        const string selectColumns = """
            SELECT fixture_id, tier, path, repo, pr_number, language,
                   rule_ids_json, tags_json, pr_size_bucket,
                   has_tests_changed, has_review_comments, source, created_at_utc
            """;

        if (tier.HasValue)
        {
            cmd.CommandText = $"{selectColumns} FROM fixtures WHERE tier = $tier";
            cmd.Parameters.AddWithValue("$tier", tier.Value.ToString());
        }
        else
        {
            cmd.CommandText = $"{selectColumns} FROM fixtures";
        }

        var results = new List<FixtureMetadata>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = ReadFixtureIndexRow(reader);
            var metadata = await TryLoadMetadataAsync(row, ct).ConfigureAwait(false);
            if (metadata is not null)
                results.Add(metadata);
        }

        return results;
    }

    private sealed record FixtureIndexRow(
        string FixtureId,
        string Tier,
        string? StoredPath,
        string Repo,
        int PullRequestNumber,
        string Language,
        string? RuleIdsJson,
        string? TagsJson,
        string? PrSizeBucket,
        bool HasTestsChanged,
        bool HasReviewComments,
        string Source,
        string CreatedAtUtc);

    private static FixtureIndexRow ReadFixtureIndexRow(SqliteDataReader reader) =>
        new(
            FixtureId: reader.GetString(0),
            Tier: reader.GetString(1),
            StoredPath: reader.IsDBNull(2) ? null : reader.GetString(2),
            Repo: reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            PullRequestNumber: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            Language: reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            RuleIdsJson: reader.IsDBNull(6) ? null : reader.GetString(6),
            TagsJson: reader.IsDBNull(7) ? null : reader.GetString(7),
            PrSizeBucket: reader.IsDBNull(8) ? null : reader.GetString(8),
            HasTestsChanged: !reader.IsDBNull(9) && reader.GetInt32(9) != 0,
            HasReviewComments: !reader.IsDBNull(10) && reader.GetInt32(10) != 0,
            Source: reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            CreatedAtUtc: reader.IsDBNull(12) ? string.Empty : reader.GetString(12));

    private async Task<FixtureMetadata?> TryLoadMetadataAsync(FixtureIndexRow row, CancellationToken ct)
    {
        foreach (var candidatePath in EnumerateFixtureDirectoryCandidates(row))
        {
            var metaPath = Path.Combine(candidatePath, "metadata.json");
            if (!File.Exists(metaPath))
                continue;

            var json = await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<FixtureMetadata>(json, JsonOpts);
        }

        return BuildMetadataFromIndexRow(row);
    }

    private IEnumerable<string> EnumerateFixtureDirectoryCandidates(FixtureIndexRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.StoredPath))
        {
            yield return row.StoredPath;
            if (!Path.IsPathRooted(row.StoredPath))
                yield return Path.GetFullPath(row.StoredPath);
        }

        if (Enum.TryParse<FixtureTier>(row.Tier, ignoreCase: true, out var tier))
            yield return FixtureIdHelper.GetFixturePath(_basePath, tier, row.FixtureId);
    }

    private static FixtureMetadata? BuildMetadataFromIndexRow(FixtureIndexRow row)
    {
        if (!Enum.TryParse<FixtureTier>(row.Tier, ignoreCase: true, out var tier))
            return null;

        Enum.TryParse<PrSizeBucket>(row.PrSizeBucket, ignoreCase: true, out var sizeBucket);
        DateTime.TryParse(row.CreatedAtUtc, out var createdAtUtc);

        var ruleIds = TryDeserializeStringList(row.RuleIdsJson);
        var tags = TryDeserializeStringList(row.TagsJson);

        return new FixtureMetadata
        {
            FixtureId = row.FixtureId,
            Tier = tier,
            Repo = row.Repo,
            PullRequestNumber = row.PullRequestNumber,
            Language = row.Language,
            RuleIds = ruleIds,
            Tags = tags,
            PrSizeBucket = sizeBucket,
            HasTestsChanged = row.HasTestsChanged,
            HasReviewComments = row.HasReviewComments,
            Source = row.Source,
            CreatedAtUtc = createdAtUtc == default ? DateTime.UtcNow : createdAtUtc,
        };
    }

    public async Task<IReadOnlyList<ExpectedFinding>> ReadExpectedFindingsAsync(
        string fixtureId, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId);
        if (fixturePath is null) return [];

        var expectedPath = Path.Combine(fixturePath, "expected.json");
        if (!File.Exists(expectedPath)) return [];

        try
        {
            var json = await File.ReadAllTextAsync(expectedPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ExpectedFinding>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<ActualFinding>> ReadActualFindingsAsync(
        string fixtureId, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId);
        if (fixturePath is null) return [];

        var actualPath = Path.Combine(fixturePath, "actual.json");
        if (!File.Exists(actualPath)) return [];

        try
        {
            var json = await File.ReadAllTextAsync(actualPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ActualFinding>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<string?> TryReadReviewCommentsAsync(
        string fixtureId, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId);
        if (fixturePath is null) return null;

        var reviewPath = Path.Combine(fixturePath, "raw", "review-comments.json");
        if (!File.Exists(reviewPath)) return null;

        return await File.ReadAllTextAsync(reviewPath, ct).ConfigureAwait(false);
    }



    private string EnsureFixtureDir(FixtureTier tier, string fixtureId)
    {
        var path = FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId);
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(FixtureIdHelper.GetRawPath(path));
        return path;
    }

    private string? FindExistingFixturePath(string fixtureId)
    {
        foreach (var tier in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
        {
            var path = FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId);
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    private static void EnsureNotesTemplate(string fixturePath, FixtureMetadata meta)
    {
        var notesPath = Path.Combine(fixturePath, "notes.md");
        if (File.Exists(notesPath)) return;

        var template = $"""
            # {meta.FixtureId}

            **Repo:** {meta.Repo}  
            **PR:** #{meta.PullRequestNumber}  
            **Tier:** {meta.Tier}  
            **Size:** {meta.PrSizeBucket} ({meta.FilesChanged} files)  

            ## Reviewer Notes

            <!-- Add notes here -->

            ## Label Justification

            <!-- Explain why expected.json was labeled the way it was -->
            """;

        File.WriteAllText(notesPath, template);
    }

    private static IReadOnlyList<string> TryDeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task UpsertFixtureSqliteAsync(FixtureMetadata meta, string fixturePath, CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO fixtures (id, fixture_id, tier, repo, pr_number, language, path,
                rule_ids_json, tags_json, pr_size_bucket, has_tests_changed,
                has_review_comments, source, created_at_utc)
            VALUES ($id, $fixture_id, $tier, $repo, $pr_number, $language, $path,
                $rule_ids_json, $tags_json, $pr_size_bucket, $has_tests_changed,
                $has_review_comments, $source, $created_at_utc)
            ON CONFLICT(fixture_id) DO UPDATE SET
                tier = excluded.tier,
                path = excluded.path,
                rule_ids_json = excluded.rule_ids_json,
                tags_json = excluded.tags_json,
                pr_size_bucket = excluded.pr_size_bucket,
                has_tests_changed = excluded.has_tests_changed,
                has_review_comments = excluded.has_review_comments;
            """;

        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$fixture_id", meta.FixtureId);
        cmd.Parameters.AddWithValue("$tier", meta.Tier.ToString());
        cmd.Parameters.AddWithValue("$repo", meta.Repo);
        cmd.Parameters.AddWithValue("$pr_number", meta.PullRequestNumber);
        cmd.Parameters.AddWithValue("$language", meta.Language);
        cmd.Parameters.AddWithValue("$path", fixturePath);
        cmd.Parameters.AddWithValue("$rule_ids_json", JsonSerializer.Serialize(meta.RuleIds));
        cmd.Parameters.AddWithValue("$tags_json", JsonSerializer.Serialize(meta.Tags));
        cmd.Parameters.AddWithValue("$pr_size_bucket", meta.PrSizeBucket.ToString());
        cmd.Parameters.AddWithValue("$has_tests_changed", meta.HasTestsChanged ? 1 : 0);
        cmd.Parameters.AddWithValue("$has_review_comments", meta.HasReviewComments ? 1 : 0);
        cmd.Parameters.AddWithValue("$source", meta.Source);
        cmd.Parameters.AddWithValue("$created_at_utc", meta.CreatedAtUtc.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
