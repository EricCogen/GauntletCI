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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ScoreAggregator(IFixtureStore store, CorpusDb db)
    {
        _store = store;
        _db    = db;
    }

    public async Task<IReadOnlyList<RuleScorecard>> ScoreAsync(
        string? ruleId = null, FixtureTier? tier = null, CancellationToken cancellationToken = default)
    {
        var fixtures     = await _store.ListFixturesAsync(tier, cancellationToken);
        var fixturePaths = await LoadFixturePathsAsync(cancellationToken);

        // (ruleId, tier) → list of (expected, actual?) pairs
        var groups = new Dictionary<(string RuleId, FixtureTier Tier), List<(ExpectedFinding Exp, ActualFinding? Act)>>();

        foreach (var fixture in fixtures)
        {
            if (!fixturePaths.TryGetValue(fixture.FixtureId, out var fixturePath)) continue;

            var expectedPath = Path.Combine(fixturePath, "expected.json");
            var actualPath   = Path.Combine(fixturePath, "actual.json");

            var expectedFindings = await ReadJsonFileAsync<List<ExpectedFinding>>(expectedPath, cancellationToken) ?? [];
            var actualFindings   = await ReadJsonFileAsync<List<ActualFinding>>(actualPath, cancellationToken)   ?? [];

            var actualByRule = actualFindings.ToDictionary(f => f.RuleId, f => f);

            foreach (var exp in expectedFindings)
            {
                var key = (exp.RuleId, fixture.Tier);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = [];
                    groups[key] = list;
                }
                actualByRule.TryGetValue(exp.RuleId, out var act);
                list.Add((exp, act));
            }
        }

        var scorecards = new List<RuleScorecard>();

        foreach (var ((rid, rtier), pairs) in groups)
        {
            if (!string.IsNullOrEmpty(ruleId) && rid != ruleId) continue;

            int total       = pairs.Count;
            int triggered   = pairs.Count(p => p.Act?.DidTrigger == true);
            int tp          = pairs.Count(p => p.Act?.DidTrigger == true && p.Exp.ShouldTrigger);
            int fp          = pairs.Count(p => p.Act?.DidTrigger == true && !p.Exp.ShouldTrigger);
            int fn          = pairs.Count(p => p.Exp.ShouldTrigger && p.Act?.DidTrigger != true);
            int inconclusive = pairs.Count(p => p.Exp.IsInconclusive);

            double triggerRate      = total > 0 ? (double)triggered / total : 0.0;
            double precision        = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0.0;
            double recall           = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0.0;
            double inconclusiveRate = total > 0 ? (double)inconclusive / total : 0.0;
            double avgUsefulness    = await GetAvgUsefulnessAsync(rid, cancellationToken);

            var scorecard = new RuleScorecard(rid, rtier, total, triggerRate, precision, recall,
                inconclusiveRate, avgUsefulness, Notes: string.Empty);

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
