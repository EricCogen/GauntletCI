// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Scoring;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Storage;

/// <summary>
/// Persists labeled rule metrics into <c>audit_snapshots</c> / <c>audit_snapshot_rows</c>.
/// </summary>
public static class AuditSnapshotWriter
{
    public static async Task<int> InsertAsync(
        SqliteConnection connection,
        IReadOnlyList<LabeledRuleMetric> rows,
        IReadOnlyDictionary<string, double?> usefulness,
        string? notes,
        CancellationToken ct = default)
    {
        var snappedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        using var insertSnapshot = connection.CreateCommand();
        insertSnapshot.CommandText = """
            INSERT INTO audit_snapshots (snapped_at_utc, rules_snapped, notes)
            VALUES ($snappedAt, $rulesSnapped, $notes)
            """;
        insertSnapshot.Parameters.AddWithValue("$snappedAt", snappedAt);
        insertSnapshot.Parameters.AddWithValue("$rulesSnapped", rows.Count);
        insertSnapshot.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        await insertSnapshot.ExecuteNonQueryAsync(ct);

        using var idCmd = connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        var snapshotIdObj = await idCmd.ExecuteScalarAsync(ct);
        var snapshotId = Convert.ToInt32(snapshotIdObj);

        using var insertRow = connection.CreateCommand();
        insertRow.CommandText = """
            INSERT INTO audit_snapshot_rows (
                snapshot_id, rule_id, labeled, tp, fp, fn,
                precision_score, recall_score, usefulness_score
            ) VALUES (
                $snapshotId, $ruleId, $labeled, $tp, $fp, $fn,
                $precision, $recall, $usefulness
            )
            """;

        var snapshotParam = insertRow.Parameters.Add("$snapshotId", SqliteType.Integer);
        var ruleParam = insertRow.Parameters.Add("$ruleId", SqliteType.Text);
        var labeledParam = insertRow.Parameters.Add("$labeled", SqliteType.Integer);
        var tpParam = insertRow.Parameters.Add("$tp", SqliteType.Integer);
        var fpParam = insertRow.Parameters.Add("$fp", SqliteType.Integer);
        var fnParam = insertRow.Parameters.Add("$fn", SqliteType.Integer);
        var precisionParam = insertRow.Parameters.Add("$precision", SqliteType.Real);
        var recallParam = insertRow.Parameters.Add("$recall", SqliteType.Real);
        var usefulnessParam = insertRow.Parameters.Add("$usefulness", SqliteType.Real);

        snapshotParam.Value = snapshotId;

        foreach (var row in rows)
        {
            usefulness.TryGetValue(row.RuleId, out var usefulnessScore);

            ruleParam.Value = row.RuleId;
            labeledParam.Value = row.Labeled;
            tpParam.Value = row.Tp;
            fpParam.Value = row.Fp;
            fnParam.Value = row.Fn;
            precisionParam.Value = row.PrecisionScore.HasValue ? row.PrecisionScore.Value : DBNull.Value;
            recallParam.Value = row.RecallScore.HasValue ? row.RecallScore.Value : DBNull.Value;
            usefulnessParam.Value = usefulnessScore.HasValue ? usefulnessScore.Value : DBNull.Value;

            await insertRow.ExecuteNonQueryAsync(ct);
        }

        return snapshotId;
    }
}
