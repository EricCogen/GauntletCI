// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Scoring;

public sealed class ScoreAggregator : IScoreAggregator
{
    private readonly IFixtureStore _store;
    private readonly CorpusDb _db;
    private readonly IEvaluationClassifier _classifier;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ScoreAggregator(IFixtureStore store, CorpusDb db)
        : this(store, db, new EvaluationClassifier()) { }

    public ScoreAggregator(IFixtureStore store, CorpusDb db, IEvaluationClassifier classifier)
    {
        _store      = store;
        _db         = db;
        _classifier = classifier;
    }

    public async Task<IReadOnlyList<RuleScorecard>> ScoreAsync(
        string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default)
    {
        var fixtures     = await _store.ListFixturesAsync(tier, cancellationToken).ConfigureAwait(false);
        var fixturePaths = await LoadFixturePathsAsync(cancellationToken).ConfigureAwait(false);

        // Totals per tier (denominator for trigger rate)
        var totalPerTier = new Dictionary<FixtureTier, int>();
        // How many fixtures each rule fired on, per tier
        var firedCounts  = new Dictionary<(string RuleId, FixtureTier Tier), int>();
        // All classification results
        var allEvaluations = new List<FindingEvaluation>();

        foreach (var fixture in fixtures)
        {
            if (!fixturePaths.TryGetValue(fixture.FixtureId, out var fixturePath)) continue;

            totalPerTier[fixture.Tier] = totalPerTier.GetValueOrDefault(fixture.Tier) + 1;

            var expectedPath = Path.Combine(fixturePath, "expected.json");
            var actualPath   = Path.Combine(fixturePath, "actual.json");

            var expectedFindings = await ReadJsonFileAsync<List<ExpectedFinding>>(expectedPath, cancellationToken).ConfigureAwait(false) ?? [];
            var actualFindings   = await ReadJsonFileAsync<List<ActualFinding>>(actualPath, cancellationToken).ConfigureAwait(false)   ?? [];

            // Track trigger counts across ALL fixtures (not just labeled ones).
            // Count each rule at most once per fixture -- trigger rate = fraction of fixtures
            // where the rule fired at least once, not total finding count.
            foreach (var firedRuleId in actualFindings.Where(a => a.DidTrigger).Select(a => a.RuleId).Distinct())
            {
                var key = (firedRuleId, fixture.Tier);
                firedCounts[key] = firedCounts.GetValueOrDefault(key) + 1;
            }

            var evaluations = _classifier.Classify(fixture, expectedFindings, actualFindings);
            allEvaluations.AddRange(evaluations);
        }

        // Group evaluations by (ruleId, tier)
        var groups = allEvaluations
            .GroupBy(e => (e.RuleId, e.Tier))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Collect all rule/tier combinations that had any activity
        var allKeys = new HashSet<(string RuleId, FixtureTier Tier)>(groups.Keys);
        foreach (var key in firedCounts.Keys) allKeys.Add(key);

        var scorecards = new List<RuleScorecard>();

        var allUsefulnessScores = await GetAllAvgUsefulnessAsync(cancellationToken).ConfigureAwait(false);

        foreach (var key in allKeys)
        {
            var (rid, rtier) = key;
            if (!string.IsNullOrEmpty(ruleId) && rid != ruleId) continue;

            groups.TryGetValue(key, out var evals);
            evals ??= [];

            int tp      = evals.Count(e => e.Status == EvaluationStatus.TruePositive);
            int fp      = evals.Count(e => e.Status == EvaluationStatus.FalsePositive);
            int fn      = evals.Count(e => e.Status == EvaluationStatus.FalseNegative);
            int tn      = evals.Count(e => e.Status == EvaluationStatus.TrueNegative);
            int unknown = evals.Count(e => e.Status == EvaluationStatus.Unknown);

            int labeled     = tp + fp + fn + tn;
            int totalTier   = totalPerTier.GetValueOrDefault(rtier, 1);
            int fired       = firedCounts.GetValueOrDefault(key, 0);

            double triggerRate = (double)fired / totalTier;
            // Precision = TP / (TP + FP);  Recall = TP / (TP + FN)
            // Guard against division by zero when no predictions or no actual positives
            double precision   = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0.0;
            double recall      = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0.0;

            double avgUsefulness = allUsefulnessScores.GetValueOrDefault(rid, 0.0);

            var scorecard = new RuleScorecard(
                RuleId:           rid,
                Tier:             rtier,
                Fixtures:         labeled,
                TriggerRate:      triggerRate,
                Precision:        precision,
                Recall:           recall,
                InconclusiveRate: 0.0,
                AvgUsefulness:    avgUsefulness,
                Notes:            string.Empty,
                TruePositives:    tp,
                FalsePositives:   fp,
                FalseNegatives:   fn,
                TrueNegatives:    tn,
                Unknown:          unknown);

            scorecards.Add(scorecard);
            await UpsertAggregateAsync(scorecard, cancellationToken).ConfigureAwait(false);
        }

        return scorecards;
    }

    // -- Private helpers -------------------------------------------------------

    private async Task<Dictionary<string, string>> LoadFixturePathsAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT fixture_id, path FROM fixtures";
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }

    private async Task<Dictionary<string, double>> GetAllAvgUsefulnessAsync(CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT rule_id, AVG(usefulness) FROM evaluations GROUP BY rule_id";
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var rId = reader.GetString(0);
            var avg = reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1);
            result[rId] = avg;
        }
        return result;
    }

    private async Task<double> GetAvgUsefulnessAsync(string ruleId, CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT AVG(usefulness) FROM evaluations WHERE rule_id = $ruleId";
        cmd.Parameters.AddWithValue("$ruleId", ruleId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is double d ? d : 0.0;
    }

    private async Task UpsertAggregateAsync(RuleScorecard sc, CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO aggregates (rule_id, tier, trigger_rate, precision_score, recall_score, usefulness_score, last_updated_utc)
            VALUES ($ruleId, $tier, $triggerRate, $precision, $recall, $usefulness, datetime('now'))
            ON CONFLICT(rule_id, tier) DO UPDATE SET
                trigger_rate     = excluded.trigger_rate,
                precision_score  = excluded.precision_score,
                recall_score     = excluded.recall_score,
                usefulness_score = excluded.usefulness_score,
                last_updated_utc = excluded.last_updated_utc;
            """;
        cmd.Parameters.AddWithValue("$ruleId",      sc.RuleId);
        cmd.Parameters.AddWithValue("$tier",        sc.Tier.ToString());
        cmd.Parameters.AddWithValue("$triggerRate", sc.TriggerRate);
        cmd.Parameters.AddWithValue("$precision",   sc.Precision);
        cmd.Parameters.AddWithValue("$recall",      sc.Recall);
        cmd.Parameters.AddWithValue("$usefulness",  sc.AvgUsefulness);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<T?> ReadJsonFileAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return default;
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }
}
