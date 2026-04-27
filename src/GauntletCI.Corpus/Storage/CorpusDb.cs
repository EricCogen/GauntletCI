// SPDX-License-Identifier: Elastic-2.0
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Storage;

/// <summary>
/// Opens (or creates) the corpus SQLite database and applies the schema.
/// Call <see cref="InitializeAsync"/> once at startup before any other DB access.
/// </summary>
public sealed class CorpusDb : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public CorpusDb(string dbPath = "./data/gauntletci-corpus.db")
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>The open SQLite connection; throws if <see cref="InitializeAsync"/> has not been called.</summary>
    public SqliteConnection Connection => _connection
        ?? throw new InvalidOperationException("Call InitializeAsync first.");

    /// <summary>
    /// Opens the SQLite connection and applies the DDL schema and any pending migrations.
    /// Must be called once before accessing <see cref="Connection"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async open and schema operations.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);
        await ApplySchemaAsync(cancellationToken);
    }

    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = SchemaInitializer.Ddl;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Idempotent migrations — ALTER TABLE errors if column exists; that is harmless.
        foreach (var migration in SchemaInitializer.Migrations)
        {
            try
            {
                using var m = Connection.CreateCommand();
                m.CommandText = migration;
                await m.ExecuteNonQueryAsync(cancellationToken);
            }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                                 || ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent — column or table already exists; safe to ignore
        }
        }
    }

    /// <summary>Disposes the underlying SQLite connection.</summary>
    public void Dispose() => _connection?.Dispose();

    public async Task LogPipelineErrorAsync(
        string step,
        string? provider = null,
        string? repo = null,
        int? errorCode = null,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pipeline_errors (step, provider, repo, error_code, message)
            VALUES ($step, $provider, $repo, $code, $message)
            """;
        cmd.Parameters.AddWithValue("$step",     step);
        cmd.Parameters.AddWithValue("$provider", (object?)provider ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$repo",     (object?)repo     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$code",     (object?)errorCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$message",  message);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal static class SchemaInitializer
{
    internal const string Ddl = """
        PRAGMA journal_mode=WAL;

        CREATE TABLE IF NOT EXISTS candidates (
            id                  TEXT PRIMARY KEY,
            source              TEXT NOT NULL,
            repo_owner          TEXT NOT NULL,
            repo_name           TEXT NOT NULL,
            pr_number           INTEGER NOT NULL,
            url                 TEXT NOT NULL,
            language            TEXT,
            created_at_utc      TEXT,
            updated_at_utc      TEXT,
            review_comment_count INTEGER DEFAULT 0,
            candidate_reason    TEXT,
            raw_metadata_json   TEXT,
            discovered_at_utc   TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE(repo_owner, repo_name, pr_number)
        );

        CREATE TABLE IF NOT EXISTS hydrations (
            id                  TEXT PRIMARY KEY,
            candidate_id        TEXT NOT NULL REFERENCES candidates(id),
            base_sha            TEXT,
            head_sha            TEXT,
            files_changed_count INTEGER DEFAULT 0,
            additions           INTEGER DEFAULT 0,
            deletions           INTEGER DEFAULT 0,
            hydrated_at_utc     TEXT,
            status              TEXT NOT NULL DEFAULT 'Pending',
            error_message       TEXT
        );

        CREATE TABLE IF NOT EXISTS repo_rejections (
            repo_owner           TEXT NOT NULL,
            repo_name            TEXT NOT NULL,
            reason               TEXT NOT NULL,
            source               TEXT NOT NULL,
            first_rejected_at_utc TEXT NOT NULL DEFAULT (datetime('now')),
            last_rejected_at_utc  TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (repo_owner, repo_name)
        );

        CREATE TABLE IF NOT EXISTS fixtures (
            id                  TEXT PRIMARY KEY,
            fixture_id          TEXT NOT NULL UNIQUE,
            tier                TEXT NOT NULL,
            repo                TEXT NOT NULL,
            pr_number           INTEGER NOT NULL,
            language            TEXT,
            path                TEXT,
            rule_ids_json       TEXT,
            tags_json           TEXT,
            pr_size_bucket      TEXT,
            has_tests_changed   INTEGER DEFAULT 0,
            has_review_comments INTEGER DEFAULT 0,
            source              TEXT,
            created_at_utc      TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS expected_findings (
            id                  TEXT PRIMARY KEY,
            fixture_id          TEXT NOT NULL REFERENCES fixtures(fixture_id),
            rule_id             TEXT NOT NULL,
            should_trigger      INTEGER NOT NULL DEFAULT 0,
            expected_confidence REAL DEFAULT 0.0,
            reason              TEXT,
            label_source        TEXT,
            is_inconclusive     INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS actual_findings (
            id                  TEXT PRIMARY KEY,
            fixture_id          TEXT NOT NULL REFERENCES fixtures(fixture_id),
            run_id              TEXT NOT NULL,
            rule_id             TEXT NOT NULL,
            did_trigger         INTEGER NOT NULL DEFAULT 0,
            actual_confidence   REAL DEFAULT 0.0,
            message             TEXT,
            change_implication  TEXT,
            evidence_json       TEXT,
            execution_time_ms   INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS rule_runs (
            id                  TEXT PRIMARY KEY,
            fixture_id          TEXT NOT NULL REFERENCES fixtures(fixture_id),
            started_at_utc      TEXT NOT NULL,
            completed_at_utc    TEXT,
            engine_version      TEXT,
            rule_set_version    TEXT,
            status              TEXT NOT NULL DEFAULT 'Pending',
            error_message       TEXT
        );

        CREATE TABLE IF NOT EXISTS evaluations (
            id                  TEXT PRIMARY KEY,
            fixture_id          TEXT NOT NULL REFERENCES fixtures(fixture_id),
            rule_id             TEXT NOT NULL,
            usefulness          REAL DEFAULT 0.0,
            reviewer_notes      TEXT,
            evaluated_at_utc    TEXT NOT NULL DEFAULT (datetime('now')),
            reviewer            TEXT
        );

        CREATE TABLE IF NOT EXISTS aggregates (
            rule_id             TEXT NOT NULL,
            tier                TEXT NOT NULL,
            trigger_rate        REAL DEFAULT 0.0,
            precision_score     REAL DEFAULT 0.0,
            recall_score        REAL DEFAULT 0.0,
            usefulness_score    REAL DEFAULT 0.0,
            last_updated_utc    TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (rule_id, tier)
        );

        CREATE TABLE IF NOT EXISTS issues (
            id              TEXT PRIMARY KEY,
            repo_owner      TEXT NOT NULL,
            repo_name       TEXT NOT NULL,
            number          INTEGER NOT NULL,
            title           TEXT,
            body            TEXT,
            labels_json     TEXT,
            state           TEXT,
            closed_at_utc   TEXT,
            url             TEXT,
            fetched_at_utc  TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE (repo_owner, repo_name, number)
        );

        CREATE TABLE IF NOT EXISTS fixture_issues (
            fixture_id      TEXT NOT NULL REFERENCES fixtures(fixture_id),
            issue_id        TEXT NOT NULL REFERENCES issues(id),
            link_source     TEXT NOT NULL DEFAULT 'pr-body-ref',
            PRIMARY KEY (fixture_id, issue_id)
        );

        CREATE TABLE IF NOT EXISTS pipeline_errors (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            step         TEXT NOT NULL,
            provider     TEXT,
            repo         TEXT,
            error_code   INTEGER,
            message      TEXT NOT NULL,
            recorded_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
        );
        """;

    internal static readonly string[] Migrations =
    [
        "ALTER TABLE actual_findings ADD COLUMN file_path TEXT",
        """
        CREATE TABLE IF NOT EXISTS dependabot_matches (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id    TEXT    NOT NULL REFERENCES fixtures(fixture_id),
            repo          TEXT    NOT NULL,
            pr_number     INTEGER NOT NULL,
            is_dependabot INTEGER NOT NULL DEFAULT 0,
            pr_title      TEXT    NOT NULL DEFAULT '',
            author_login  TEXT    NOT NULL DEFAULT '',
            fetched_at_utc TEXT   NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS social_signal_enrichments (
            id                   INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id           TEXT    NOT NULL REFERENCES fixtures(fixture_id),
            repo                 TEXT    NOT NULL,
            pr_number            INTEGER NOT NULL,
            review_time_minutes  REAL    NOT NULL DEFAULT -1,
            reviewer_count       INTEGER NOT NULL DEFAULT 0,
            review_comment_count INTEGER NOT NULL DEFAULT 0,
            is_bot_merged        INTEGER NOT NULL DEFAULT 0,
            social_signal_score  REAL    NOT NULL DEFAULT 0.0,
            fetched_at_utc       TEXT    NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS composite_labels (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id        TEXT    NOT NULL REFERENCES fixtures(fixture_id),
            composite_label   TEXT    NOT NULL,
            label_confidence  REAL    NOT NULL DEFAULT 0.0,
            signals_json      TEXT    NOT NULL DEFAULT '{}',
            applied_at_utc    TEXT    NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS sonar_matches (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id        TEXT NOT NULL REFERENCES fixtures(fixture_id),
            sonar_project_key TEXT NOT NULL,
            changed_file      TEXT NOT NULL,
            sonar_rule        TEXT NOT NULL DEFAULT '',
            sonar_severity    TEXT NOT NULL DEFAULT '',
            sonar_type        TEXT NOT NULL DEFAULT '',
            sonar_message     TEXT DEFAULT '',
            fetched_at_utc    TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id, changed_file, sonar_rule)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS code_scanning_matches (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id       TEXT NOT NULL REFERENCES fixtures(fixture_id),
            repo             TEXT NOT NULL,
            changed_file     TEXT NOT NULL,
            codeql_rule      TEXT NOT NULL DEFAULT '',
            codeql_rule_name TEXT NOT NULL DEFAULT '',
            alert_state      TEXT NOT NULL DEFAULT '',
            tool_name        TEXT NOT NULL DEFAULT '',
            severity         TEXT NOT NULL DEFAULT '',
            start_line       INTEGER NOT NULL DEFAULT 0,
            message          TEXT DEFAULT '',
            fetched_at_utc   TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id, changed_file, codeql_rule)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS semgrep_enrichments (
            fixture_id        TEXT NOT NULL,
            repo              TEXT NOT NULL,
            finding_count     INTEGER NOT NULL DEFAULT 0,
            rules_fired       TEXT,
            highest_severity  TEXT,
            findings_json     TEXT,
            scanned_at_utc    TEXT NOT NULL,
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS structural_enrichments (
            fixture_id            TEXT NOT NULL,
            repo                  TEXT NOT NULL,
            changed_files_json    TEXT,
            sensitive_file_count  INTEGER NOT NULL DEFAULT 0,
            max_file_churn_30d    INTEGER NOT NULL DEFAULT 0,
            structural_risk_score REAL NOT NULL DEFAULT 0.0,
            fetched_at_utc        TEXT NOT NULL,
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS nuget_advisory_enrichments (
            fixture_id           TEXT NOT NULL,
            repo                 TEXT NOT NULL,
            packages_checked     INTEGER NOT NULL DEFAULT 0,
            advisory_count       INTEGER NOT NULL DEFAULT 0,
            highest_severity     TEXT,
            advisories_json      TEXT,
            scanned_at_utc       TEXT NOT NULL,
            UNIQUE(fixture_id)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS file_churn_enrichments (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id        TEXT NOT NULL,
            repo              TEXT NOT NULL,
            file_path         TEXT NOT NULL,
            churn_90d         INTEGER NOT NULL DEFAULT 0,
            hotspot_score     REAL NOT NULL DEFAULT 0.0,
            fetched_at_utc    TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id, file_path)
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS review_comment_nlp_enrichments (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            fixture_id        TEXT NOT NULL,
            repo              TEXT NOT NULL,
            matched_rule_id   TEXT NOT NULL,
            matched_keyword   TEXT NOT NULL,
            confidence        REAL NOT NULL DEFAULT 0.0,
            fetched_at_utc    TEXT NOT NULL DEFAULT (datetime('now')),
            UNIQUE(fixture_id, matched_rule_id)
        )
        """,
    ];
}
