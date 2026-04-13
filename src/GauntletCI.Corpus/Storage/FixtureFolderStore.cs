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
        Converters = { new JsonStringEnumConverter() },
    };

    public FixtureFolderStore(CorpusDb db, string basePath = "./data/fixtures")
    {
        _db = db;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    // ── IFixtureStore ────────────────────────────────────────────────────────

    public async Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken ct = default)
    {
        var fixturePath = EnsureFixtureDir(metadata.Tier, metadata.FixtureId);
        var metaPath    = Path.Combine(fixturePath, "metadata.json");

        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(metadata, JsonOpts), ct);
        EnsureNotesTemplate(fixturePath, metadata);
        await UpsertFixtureSqliteAsync(metadata, fixturePath, ct);
    }

    public async Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken ct = default)
    {
        // Try each tier in preference order: gold > silver > discovery
        foreach (var tier in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
        {
            var path = Path.Combine(FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId), "metadata.json");
            if (!File.Exists(path)) continue;

            var json = await File.ReadAllTextAsync(path, ct);
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
        await File.WriteAllTextAsync(expectedPath, JsonSerializer.Serialize(findings, JsonOpts), ct);
    }

    public async Task SaveActualFindingsAsync(
        string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken ct = default)
    {
        var fixturePath = FindExistingFixturePath(fixtureId)
            ?? throw new InvalidOperationException($"Fixture '{fixtureId}' not found. Call SaveMetadataAsync first.");

        // Actual findings are stored per run so prior runs aren't overwritten.
        var actualPath = Path.Combine(fixturePath, $"actual.{runId}.json");
        await File.WriteAllTextAsync(actualPath, JsonSerializer.Serialize(findings, JsonOpts), ct);

        // Also write/overwrite the canonical actual.json with the latest run.
        var latestPath = Path.Combine(fixturePath, "actual.json");
        await File.WriteAllTextAsync(latestPath, JsonSerializer.Serialize(findings, JsonOpts), ct);
    }

    public async Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(
        FixtureTier? tier = null, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();

        if (tier.HasValue)
        {
            cmd.CommandText = "SELECT path FROM fixtures WHERE tier = $tier";
            cmd.Parameters.AddWithValue("$tier", tier.Value.ToString());
        }
        else
        {
            cmd.CommandText = "SELECT path FROM fixtures";
        }

        var paths = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            paths.Add(reader.GetString(0));

        var results = new List<FixtureMetadata>();
        foreach (var path in paths)
        {
            var metaPath = Path.Combine(path, "metadata.json");
            if (!File.Exists(metaPath)) continue;
            var json = await File.ReadAllTextAsync(metaPath, ct);
            var m = JsonSerializer.Deserialize<FixtureMetadata>(json, JsonOpts);
            if (m is not null) results.Add(m);
        }
        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
