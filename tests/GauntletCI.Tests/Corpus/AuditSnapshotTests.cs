// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Scoring;
using GauntletCI.Corpus.Storage;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Tests.Corpus;

public class AuditSnapshotTests
{
    [Fact]
    public async Task LabeledRuleMetricsReader_ComputesTpFpFn_FromLatestCompletedRun()
    {
        var tempDir = CreateTempDir();
        try
        {
            var db = new CorpusDb(Path.Combine(tempDir, "corpus.db"));
            await db.InitializeAsync(CancellationToken.None);
            using (db)
            {
                await SeedMinimalFixtureAsync(db.Connection);

                var metrics = await LabeledRuleMetricsReader.ReadAsync(db.Connection);
                var gci0004 = Assert.Single(metrics, m => m.RuleId == "GCI0004");

                Assert.Equal(2, gci0004.Labeled);
                Assert.Equal(1, gci0004.Tp);
                Assert.Equal(1, gci0004.Fp);
                Assert.Equal(0, gci0004.Fn);
                Assert.Equal(0.5, gci0004.PrecisionScore);
                Assert.Equal(1.0, gci0004.RecallScore);
            }
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public async Task AuditSnapshotWriter_InsertsSnapshotAndRows()
    {
        var tempDir = CreateTempDir();
        try
        {
            var db = new CorpusDb(Path.Combine(tempDir, "corpus.db"));
            await db.InitializeAsync(CancellationToken.None);
            using (db)
            {
                await SeedMinimalFixtureAsync(db.Connection);

                var rows = await LabeledRuleMetricsReader.ReadAsync(db.Connection);
                var usefulness = await LabeledRuleMetricsReader.ReadUsefulnessAsync(db.Connection);

                var snapshotId = await AuditSnapshotWriter.InsertAsync(
                    db.Connection,
                    rows,
                    usefulness,
                    "unit test",
                    CancellationToken.None);

                Assert.True(snapshotId > 0);

                using var countCmd = db.Connection.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM audit_snapshot_rows WHERE snapshot_id = $id";
                countCmd.Parameters.AddWithValue("$id", snapshotId);
                var rowCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                Assert.Equal(rows.Count, rowCount);
            }
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void CleanupTempDir(string tempDir)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private static async Task SeedMinimalFixtureAsync(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();

        InsertFixture(connection, "fix1", "discovery", "org/repo", 1);
        InsertRuleRun(connection, "run-old", "fix1", "2026-06-01T00:00:00Z", "Completed");
        InsertRuleRun(connection, "run-new", "fix1", "2026-06-02T00:00:00Z", "Completed");

        InsertExpected(connection, "ef1", "fix1", "GCI0004", shouldTrigger: 1);
        InsertExpected(connection, "ef2", "fix1", "GCI0004", shouldTrigger: 0);

        InsertActual(connection, "af1", "fix1", "run-new", "GCI0004", didTrigger: 1);
        InsertActual(connection, "af2", "fix1", "run-new", "GCI0004", didTrigger: 1);

        await tx.CommitAsync();
    }

    private static void InsertFixture(SqliteConnection connection, string fixtureId, string tier, string repo, int prNumber)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO fixtures (id, fixture_id, tier, repo, pr_number)
            VALUES ($id, $fixtureId, $tier, $repo, $prNumber)
            """;
        cmd.Parameters.AddWithValue("$id", fixtureId);
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$tier", tier);
        cmd.Parameters.AddWithValue("$repo", repo);
        cmd.Parameters.AddWithValue("$prNumber", prNumber);
        cmd.ExecuteNonQuery();
    }

    private static void InsertRuleRun(
        SqliteConnection connection,
        string runId,
        string fixtureId,
        string completedAtUtc,
        string status)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rule_runs (id, fixture_id, started_at_utc, completed_at_utc, status)
            VALUES ($id, $fixtureId, $started, $completed, $status)
            """;
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$started", completedAtUtc);
        cmd.Parameters.AddWithValue("$completed", completedAtUtc);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.ExecuteNonQuery();
    }

    private static void InsertExpected(
        SqliteConnection connection,
        string id,
        string fixtureId,
        string ruleId,
        int shouldTrigger)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO expected_findings (id, fixture_id, rule_id, should_trigger, is_inconclusive)
            VALUES ($id, $fixtureId, $ruleId, $shouldTrigger, 0)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$ruleId", ruleId);
        cmd.Parameters.AddWithValue("$shouldTrigger", shouldTrigger);
        cmd.ExecuteNonQuery();
    }

    private static void InsertActual(
        SqliteConnection connection,
        string id,
        string fixtureId,
        string runId,
        string ruleId,
        int didTrigger)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO actual_findings (id, fixture_id, run_id, rule_id, did_trigger)
            VALUES ($id, $fixtureId, $runId, $ruleId, $didTrigger)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$runId", runId);
        cmd.Parameters.AddWithValue("$ruleId", ruleId);
        cmd.Parameters.AddWithValue("$didTrigger", didTrigger);
        cmd.ExecuteNonQuery();
    }
}
