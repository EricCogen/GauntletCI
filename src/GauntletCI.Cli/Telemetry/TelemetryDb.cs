// SPDX-License-Identifier: Elastic-2.0
using Microsoft.Data.Sqlite;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// SQLite-backed local event store for rule-level telemetry.
/// Implements the GauntletCI Moat Spec §2.1 events table schema.
/// Path: ~/.gauntletci/telemetry.db
///
/// Runs alongside TelemetryStore (JSON upload queue). TelemetryDb is the
/// durable local analytics store: events are retained even for Local/Off users.
/// </summary>
public static class TelemetryDb
{
    internal static readonly string DefaultDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "telemetry.db");

    private static readonly SemaphoreSlim _guard = new(1, 1);

    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS events (
            event_id           TEXT PRIMARY KEY,
            event_type         TEXT NOT NULL,
            timestamp          INTEGER NOT NULL,
            install_id         TEXT,
            git_repo_hash      TEXT,
            rule_id            TEXT,
            finding_hash       TEXT,
            user_action        TEXT,
            language           TEXT,
            diff_added_lines   INTEGER,
            diff_deleted_lines INTEGER,
            llm_enabled        INTEGER DEFAULT 0,
            finding_count      INTEGER,
            files_changed      INTEGER,
            rules_evaluated    INTEGER,
            lines_added        INTEGER,
            lines_removed      INTEGER,
            confidence         TEXT,
            file_ext           TEXT,
            duration_ms        INTEGER,
            outcome            TEXT,
            vote               TEXT,
            sent               INTEGER DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS idx_events_sent    ON events(sent);
        CREATE INDEX IF NOT EXISTS idx_events_rule_id ON events(rule_id);
        CREATE INDEX IF NOT EXISTS idx_events_ts      ON events(timestamp);
        """;

    public static async Task AppendAsync(TelemetryEvent evt, string? dbPath = null)
    {
        await _guard.WaitAsync().ConfigureAwait(false);
        try
        {
            using var conn = Open(dbPath ?? DefaultDbPath);
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO events (
                    event_id, event_type, timestamp, install_id, git_repo_hash,
                    rule_id, finding_count, files_changed, rules_evaluated,
                    lines_added, lines_removed, confidence, file_ext,
                    duration_ms, outcome, vote, sent
                ) VALUES (
                    $event_id, $event_type, $timestamp, $install_id, $git_repo_hash,
                    $rule_id, $finding_count, $files_changed, $rules_evaluated,
                    $lines_added, $lines_removed, $confidence, $file_ext,
                    $duration_ms, $outcome, $vote, $sent
                )
                """;
            cmd.Parameters.AddWithValue("$event_id",        evt.EventId);
            cmd.Parameters.AddWithValue("$event_type",      evt.EventType);
            cmd.Parameters.AddWithValue("$timestamp",       evt.Timestamp.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$install_id",      (object?)evt.InstallId      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$git_repo_hash",   (object?)evt.RepoHash       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rule_id",         (object?)evt.RuleId         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$finding_count",   (object?)evt.FindingCount   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$files_changed",   (object?)evt.FilesChanged   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rules_evaluated", (object?)evt.RulesEvaluated ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lines_added",     (object?)evt.LinesAdded     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lines_removed",   (object?)evt.LinesRemoved   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$confidence",      (object?)evt.Confidence     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$file_ext",        (object?)evt.FileExt        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$duration_ms",     (object?)evt.DurationMs     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$outcome",         (object?)evt.Outcome        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$vote",            (object?)evt.Vote           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$sent",            evt.Sent ? 1 : 0);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch { /* telemetry must never crash the tool */ }
        finally { _guard.Release(); }
    }

    public static async Task MarkSentAsync(IEnumerable<string> eventIds, string? dbPath = null)
    {
        await _guard.WaitAsync().ConfigureAwait(false);
        try
        {
            using var conn = Open(dbPath ?? DefaultDbPath);
            using var tx   = conn.BeginTransaction();

            foreach (var id in eventIds)
            {
                using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE events SET sent = 1 WHERE event_id = $id";
                upd.Parameters.AddWithValue("$id", id);
                await upd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            var cutoff = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            using var purge = conn.CreateCommand();
            purge.CommandText = "DELETE FROM events WHERE sent = 1 AND timestamp < $cutoff";
            purge.Parameters.AddWithValue("$cutoff", cutoff);
            await purge.ExecuteNonQueryAsync().ConfigureAwait(false);

            tx.Commit();
        }
        catch { /* non-fatal */ }
        finally { _guard.Release(); }
    }

    public static async Task<int> CountAsync(string? dbPath = null)
    {
        await _guard.WaitAsync().ConfigureAwait(false);
        try
        {
            using var conn = Open(dbPath ?? DefaultDbPath);
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events";
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt32(result);
        }
        catch { return 0; }
        finally { _guard.Release(); }
    }

    private static SqliteConnection Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = Ddl;
        cmd.ExecuteNonQuery();
        return conn;
    }
}
