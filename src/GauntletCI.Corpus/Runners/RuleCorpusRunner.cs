// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Runners;

/// <summary>
/// Runs all registered GCI rules against a fixture diff and persists the results.
/// </summary>
public sealed class RuleCorpusRunner
{
    private readonly IFixtureStore _store;
    private readonly CorpusDb _db;
    private readonly GauntletConfig? _config;
    private readonly string? _repoPath;

    /// <summary>The run ID from the most recent call to <see cref="RunAsync"/>.</summary>
    public string LastRunId { get; private set; } = string.Empty;

    /// <param name="config">
    /// Optional GauntletCI configuration; when supplied, disabled rules (e.g. <c>"enabled": false</c>
    /// in <c>.gauntletci.json</c>) are excluded from corpus evaluation runs.
    /// </param>
    /// <param name="repoPath">
    /// Optional path to the git root; when supplied, <c>.editorconfig</c> severity overrides are
    /// applied consistently with <c>gauntletci analyze</c>.
    /// </param>
    public RuleCorpusRunner(IFixtureStore store, CorpusDb db, GauntletConfig? config = null, string? repoPath = null)
    {
        _store    = store;
        _db       = db;
        _config   = config;
        _repoPath = repoPath;
    }

    public async Task<IReadOnlyList<ActualFinding>> RunAsync(
        string fixtureId, string diffText, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var runId     = Guid.NewGuid().ToString();
        LastRunId     = runId;

        var diff   = DiffParser.Parse(diffText);
        var result = await RuleOrchestrator.CreateDefault(_config, repoPath: _repoPath).RunAsync(diff, null, null, cancellationToken).ConfigureAwait(false);

        var findings = result.Findings
            .Select(f => new ActualFinding
            {
                RuleId            = f.RuleId,
                DidTrigger        = true,
                ActualConfidence  = f.Confidence switch
                {
                    Confidence.High   => 1.0,
                    Confidence.Medium => 0.5,
                    _                 => 0.25,
                },
                Message           = f.Summary,
                ChangeImplication = f.WhyItMatters,
                Evidence          = f.Evidence,
                FilePath          = f.FilePath,
                ExecutionTimeMs   = 0,
            })
            .ToList();

        var completedAt = DateTime.UtcNow;

        await WriteRuleRunAsync(runId, fixtureId, startedAt, completedAt, cancellationToken).ConfigureAwait(false);
        await WriteActualFindingsAsync(fixtureId, runId, findings, cancellationToken).ConfigureAwait(false);
        await _store.SaveActualFindingsAsync(fixtureId, runId, findings, cancellationToken).ConfigureAwait(false);

        return findings;
    }

    // ── DB helpers ────────────────────────────────────────────────────────────

    private async Task WriteRuleRunAsync(
        string runId, string fixtureId,
        DateTime startedAt, DateTime completedAt,
        CancellationToken ct)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rule_runs (id, fixture_id, started_at_utc, completed_at_utc, engine_version, status)
            VALUES ($id, $fixture_id, $started, $completed, $version, 'Completed')
            """;
        cmd.Parameters.AddWithValue("$id",         runId);
        cmd.Parameters.AddWithValue("$fixture_id", fixtureId);
        cmd.Parameters.AddWithValue("$started",    startedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$completed",  completedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$version",
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task WriteActualFindingsAsync(
        string fixtureId, string runId,
        IReadOnlyList<ActualFinding> findings,
        CancellationToken ct)
    {
        foreach (var f in findings)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO actual_findings
                    (id, fixture_id, run_id, rule_id, did_trigger, actual_confidence,
                     message, change_implication, evidence_json, execution_time_ms, file_path)
                VALUES
                    ($id, $fixture_id, $run_id, $rule_id, $did_trigger, $actual_confidence,
                     $message, $change_implication, $evidence_json, $execution_time_ms, $file_path)
                """;
            cmd.Parameters.AddWithValue("$id",                Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$fixture_id",        fixtureId);
            cmd.Parameters.AddWithValue("$run_id",            runId);
            cmd.Parameters.AddWithValue("$rule_id",           f.RuleId);
            cmd.Parameters.AddWithValue("$did_trigger",       f.DidTrigger ? 1 : 0);
            cmd.Parameters.AddWithValue("$actual_confidence", f.ActualConfidence);
            cmd.Parameters.AddWithValue("$message",           f.Message);
            cmd.Parameters.AddWithValue("$change_implication",f.ChangeImplication);
            cmd.Parameters.AddWithValue("$evidence_json",     JsonSerializer.Serialize(f.Evidence));
            cmd.Parameters.AddWithValue("$execution_time_ms", f.ExecutionTimeMs);
            cmd.Parameters.AddWithValue("$file_path",         f.FilePath ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
