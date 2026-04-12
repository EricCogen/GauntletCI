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
        var fixtures     = await _store.ListFixturesAsync(tier, cancellationToken);
        var fixturePaths = await LoadFixturePathsAsync(cancellationToken);

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

            var expectedFindings = await ReadJsonFileAsync<List<ExpectedFinding>>(expectedPath, cancellationToken) ?? [];
            var actualFindings   = await ReadJsonFileAsync<List<ActualFinding>>(actualPath, cancellationToken)   ?? [];

            // Track trigger counts across ALL fixtures (not just labeled ones)
            foreach (var actual in actualFindings.Where(a => a.DidTrigger))
            {
                var key = (actual.RuleId, fixture.Tier);
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
            double precision   = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0.0;
            double recall      = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0.0;

            // Inconclusive rate relative to labeled pairs
            int inconclusive = evals.Count(e =>
                e.Status is EvaluationStatus.TruePositive or EvaluationStatus.FalsePositive
                         or EvaluationStatus.FalseNegative or EvaluationStatus.TrueNegative
                         or EvaluationStatus.Unknown);
            double inconclusiveRate = 0.0; // retained for schema compat; Unknown is now its own field

            double avgUsefulness = await GetAvgUsefulnessAsync(rid, cancellationToken);

            var scorecard = new RuleScorecard(
                RuleId:           rid,
                Tier:             rtier,
                Fixtures:         labeled,
                TriggerRate:      triggerRate,
                Precision:        precision,
                Recall:           recall,
                InconclusiveRate: inconclusiveRate,
                AvgUsefulness:    avgUsefulness,
                Notes:            string.Empty,
                TruePositives:    tp,
                FalsePositives:   fp,
                FalseNegatives:   fn,
                TrueNegatives:    tn,
                Unknown:          unknown);

            scorecards.Add(scorecard);
            await UpsertAggregateAsync(scorecard, cancellationToken);
        }

        return scorecards;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Dictionary<string, string>> LoadFixturePathsAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT fixture_id, path FROM fixtures";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    private async Task<double> GetAvgUsefulnessAsync(string ruleId, CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT AVG(usefulness) FROM evaluations WHERE rule_id = $ruleId";
        cmd.Parameters.AddWithValue("$ruleId", ruleId);
        var result = await cmd.ExecuteScalarAsync(ct);
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
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<T?> ReadJsonFileAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return default;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }
}
