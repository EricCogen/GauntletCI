// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Pure-diff enricher that detects Entity Framework Core migration files and SQL DDL changes.
/// Combines path-based detection (migration timestamps, .sql files, snapshots) with
/// content-based detection (migrationBuilder calls, SQL DDL keywords, EF data annotations).
/// Results are written to the <c>ef_migration_enrichments</c> table.
/// </summary>
public sealed class EFMigrationEnricher
{
    // Matches EF migration filenames: 14-digit timestamp prefix e.g. "20230101120000_AddUserTable.cs"
    private static readonly Regex MigrationFileNameRegex =
        new(@"^\d{14}_.*\.cs$", RegexOptions.Compiled);

    private static readonly string[] DdlKeywords =
    [
        "CREATE TABLE", "ALTER TABLE", "DROP TABLE",
        "DROP COLUMN", "ADD COLUMN", "ALTER COLUMN",
        "CREATE INDEX", "DROP INDEX",
    ];

    private static readonly string[] EfAnnotations =
    [
        "[Table(", "[Column(", "[ForeignKey(", "[Index(",
    ];

    public static async Task<EfMigrationResult> EnrichAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        string fixturesBasePath,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        int processed = 0, migrationFixtures = 0;

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
                processed++;
                continue;
            }

            var diffLines = await File.ReadAllLinesAsync(diffPath, ct).ConfigureAwait(false);
            var signals   = Detect(diffLines);

            await WriteEnrichmentAsync(db, fixture.FixtureId, fixture.Repo, signals, ct).ConfigureAwait(false);

            processed++;

            if (signals.MigrationDetected)
            {
                migrationFixtures++;
                progress?.Invoke(
                    $"[ef-migration] {fixture.FixtureId}: detected " +
                    $"(conf={signals.MigrationConfidence:F2}, " +
                    $"migFile={signals.HasMigrationFile}, sql={signals.HasSqlFile}, " +
                    $"efContent={signals.HasEfContent}, ddl={signals.HasDdlContent})");
            }
        }

        return new EfMigrationResult(processed, migrationFixtures);
    }

    // ── detection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Detects EF migration and DDL signals in a set of diff lines.
    /// </summary>
    internal static EfMigrationSignals Detect(IEnumerable<string> diffLines)
    {
        bool hasMigrationFile   = false;
        bool hasSqlFile         = false;
        bool hasSnapshot        = false;
        bool hasEfContent       = false;
        bool hasDdlContent      = false;
        bool hasSchemaAnnotation = false;

        string? currentFile = null;

        foreach (var line in diffLines)
        {
            // Track current file path
            if (line.StartsWith("diff --git a/", StringComparison.Ordinal))
            {
                var rest   = line[13..];
                var spaceB = rest.IndexOf(" b/", StringComparison.Ordinal);
                currentFile    = spaceB >= 0 ? rest[..spaceB] : rest;

                // Rule 1: EF migration file - path contains /Migrations/ and filename matches timestamp pattern
                if (IsMigrationFilePath(currentFile))
                    hasMigrationFile = true;

                // Rule 2: SQL file
                if (currentFile.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                    hasSqlFile = true;

                // Rule 3: EF snapshot
                if (IsSnapshotFile(currentFile))
                    hasSnapshot = true;

                continue;
            }

            // Content-based detection on added lines only
            if (line.StartsWith("+", StringComparison.Ordinal) &&
                !line.StartsWith("+++", StringComparison.Ordinal))
            {
                var content = line[1..];

                // Rule 4: EF migration class declaration
                if (content.Contains("public partial class", StringComparison.Ordinal) &&
                    (content.Contains("Migration", StringComparison.Ordinal) ||
                     content.Contains(": Migration", StringComparison.Ordinal)))
                    hasEfContent = true;

                // Rule 5: SQL DDL keywords (case-insensitive)
                if (ContainsDdlKeyword(content))
                    hasDdlContent = true;

                // Rule 6: migrationBuilder method calls
                if (content.Contains("migrationBuilder.", StringComparison.Ordinal))
                    hasEfContent = true;

                // Rule 7: EF data annotations
                if (ContainsEfAnnotation(content))
                    hasSchemaAnnotation = true;
            }
        }

        bool migrationDetected = hasMigrationFile || hasSqlFile || hasSnapshot ||
                                 hasEfContent || hasDdlContent || hasSchemaAnnotation;

        double confidence = ComputeConfidence(hasMigrationFile, hasSqlFile, hasDdlContent,
                                              hasEfContent, hasSchemaAnnotation);

        return new EfMigrationSignals(
            migrationDetected, hasMigrationFile, hasSqlFile,
            hasEfContent, hasDdlContent, confidence);
    }

    internal static bool IsMigrationFilePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            return false;
        var fileName = Path.GetFileName(normalized);
        return MigrationFileNameRegex.IsMatch(fileName);
    }

    internal static bool IsSnapshotFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith("ContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("DbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsDdlKeyword(string line)
    {
        foreach (var keyword in DdlKeywords)
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    internal static bool ContainsEfAnnotation(string line)
    {
        foreach (var annotation in EfAnnotations)
            if (line.Contains(annotation, StringComparison.Ordinal))
                return true;
        return false;
    }

    internal static double ComputeConfidence(
        bool hasMigrationFile, bool hasSqlFile, bool hasDdlContent,
        bool hasEfContent, bool hasSchemaAnnotation)
    {
        if (hasMigrationFile) return 0.95;
        if (hasSqlFile || hasDdlContent) return 0.85;
        if (hasEfContent || hasSchemaAnnotation) return 0.75;
        return 0.0;
    }

    // ── DB write ──────────────────────────────────────────────────────────────

    private static async Task WriteEnrichmentAsync(
        CorpusDb db, string fixtureId, string repo,
        EfMigrationSignals s, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO ef_migration_enrichments
                (fixture_id, repo, migration_detected, has_migration_file, has_sql_file,
                 has_ef_content, has_ddl_content, migration_confidence)
            VALUES
                ($fixtureId, $repo, $detected, $migFile, $sqlFile,
                 $efContent, $ddlContent, $confidence)
            """;
        cmd.Parameters.AddWithValue("$fixtureId",   fixtureId);
        cmd.Parameters.AddWithValue("$repo",         repo);
        cmd.Parameters.AddWithValue("$detected",     s.MigrationDetected ? 1 : 0);
        cmd.Parameters.AddWithValue("$migFile",      s.HasMigrationFile  ? 1 : 0);
        cmd.Parameters.AddWithValue("$sqlFile",      s.HasSqlFile        ? 1 : 0);
        cmd.Parameters.AddWithValue("$efContent",    s.HasEfContent      ? 1 : 0);
        cmd.Parameters.AddWithValue("$ddlContent",   s.HasDdlContent     ? 1 : 0);
        cmd.Parameters.AddWithValue("$confidence",   s.MigrationConfidence);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Detection signals for a single fixture diff.</summary>
internal record EfMigrationSignals(
    bool   MigrationDetected,
    bool   HasMigrationFile,
    bool   HasSqlFile,
    bool   HasEfContent,
    bool   HasDdlContent,
    double MigrationConfidence);

/// <summary>Summary statistics from a <see cref="EFMigrationEnricher.EnrichAsync"/> run.</summary>
public record EfMigrationResult(int FixturesProcessed, int MigrationFixtures);
