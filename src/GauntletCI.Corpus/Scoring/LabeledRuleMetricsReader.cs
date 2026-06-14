// SPDX-License-Identifier: Elastic-2.0
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Scoring;

/// <summary>
/// Labeled TP/FP/FN per rule from <c>expected_findings</c> vs latest completed run.
/// Matches agent script <c>corpus_db_read.compute_labeled_rule_metrics</c> (EvaluationClassifier parity).
/// </summary>
public sealed record LabeledRuleMetric(
    string RuleId,
    int Labeled,
    int Tp,
    int Fp,
    int Fn,
    double? PrecisionScore,
    double? RecallScore);

public static class LabeledRuleMetricsReader
{
    private const string LatestRunCte = """
        WITH latest AS (
            SELECT fixture_id, id AS run_id
            FROM (
                SELECT fixture_id,
                       id,
                       ROW_NUMBER() OVER (
                           PARTITION BY fixture_id
                           ORDER BY completed_at_utc DESC, id DESC
                       ) AS rn
                FROM rule_runs
                WHERE UPPER(status) = 'COMPLETED'
            )
            WHERE rn = 1
        )
        """;

    private const string MetricsSql = """
        , pairs AS (
            SELECT ef.rule_id,
                   ef.fixture_id,
                   ef.should_trigger,
                   COALESCE(MAX(af.did_trigger), 0) AS did_trigger
            FROM expected_findings ef
            INNER JOIN latest lr ON lr.fixture_id = ef.fixture_id
            LEFT JOIN actual_findings af
              ON af.fixture_id = ef.fixture_id
             AND af.rule_id = ef.rule_id
             AND af.run_id = lr.run_id
            WHERE COALESCE(ef.is_inconclusive, 0) = 0
            GROUP BY ef.rule_id, ef.fixture_id, ef.should_trigger
        )
        SELECT rule_id,
               COUNT(*) AS labeled,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 1 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN should_trigger = 0 AND did_trigger = 1 THEN 1 ELSE 0 END) AS fp,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 0 THEN 1 ELSE 0 END) AS fn
        FROM pairs
        GROUP BY rule_id
        ORDER BY rule_id
        """;

    public static async Task<IReadOnlyList<LabeledRuleMetric>> ReadAsync(
        SqliteConnection connection,
        CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = LatestRunCte + MetricsSql;

        var rows = new List<LabeledRuleMetric>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var ruleId = reader.GetString(0);
            var labeled = reader.GetInt32(1);
            var tp = reader.GetInt32(2);
            var fp = reader.GetInt32(3);
            var fn = reader.GetInt32(4);

            double? precision = tp + fp > 0 ? Math.Round(tp / (double)(tp + fp), 3) : null;
            double? recall = tp + fn > 0 ? Math.Round(tp / (double)(tp + fn), 3) : null;

            rows.Add(new LabeledRuleMetric(ruleId, labeled, tp, fp, fn, precision, recall));
        }

        return rows;
    }

    public static async Task<IReadOnlyDictionary<string, double?>> ReadUsefulnessAsync(
        SqliteConnection connection,
        CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT rule_id, AVG(usefulness)
            FROM evaluations
            GROUP BY rule_id
            """;

        var map = new Dictionary<string, double?>(StringComparer.Ordinal);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var ruleId = reader.GetString(0);
            double? usefulness = reader.IsDBNull(1) ? null : Math.Round(reader.GetDouble(1), 3);
            map[ruleId] = usefulness;
        }

        return map;
    }
}
