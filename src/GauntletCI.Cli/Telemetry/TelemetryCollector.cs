// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// Orchestrates post-analysis telemetry: consent check → event creation → local store → async upload.
/// Call after every successful analysis. Always safe — all failures are silently swallowed.
/// </summary>
public static class TelemetryCollector
{
    /// <summary>
    /// Collects telemetry for a completed analysis run: one summary event, one event per finding,
    /// and one event per rule metric. Triggers background upload when mode is Shared.
    /// </summary>
    /// <param name="result">The evaluation result to record.</param>
    /// <param name="diff">The diff context used in the analysis, for line-count metrics.</param>
    /// <param name="repoRoot">Absolute path to the repository root, hashed before storage.</param>
    /// <param name="quiet">Reserved for future suppression of consent prompts (currently unused).</param>
    public static async Task CollectAsync(
        EvaluationResult result,
        DiffContext diff,
        string repoRoot,
        bool quiet = false)
    {
        try
        {
            var mode = TelemetryConsent.GetMode();
            if (mode == TelemetryMode.Off) return;

            var installId = TelemetryConsent.InstallId;
            var repoHash  = await TelemetryHasher.HashRepoAsync(repoRoot);
            var linesAdded   = diff.Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count(l => l.Kind == DiffLineKind.Added)));
            var linesRemoved = diff.Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count(l => l.Kind == DiffLineKind.Removed)));

            // 1 summary event per analysis run
            await AppendAsync(new TelemetryEvent
            {
                EventType      = "analysis",
                InstallId      = installId,
                RepoHash       = repoHash,
                FindingCount   = result.Findings.Count,
                FilesChanged   = diff.Files.Count,
                RulesEvaluated = result.RulesEvaluated,
                LinesAdded     = linesAdded,
                LinesRemoved   = linesRemoved,
            });

            // 1 event per finding (rule signal — most valuable for the model)
            foreach (var finding in result.Findings)
            {
                await AppendAsync(new TelemetryEvent
                {
                    EventType  = "finding",
                    InstallId  = installId,
                    RepoHash   = repoHash,
                    RuleId     = finding.RuleId,
                    Confidence = finding.Confidence.ToString(),
                    FileExt    = ExtractExt(finding.Evidence),
                });
            }

            // 1 event per rule — timing and outcome for model training / perf monitoring
            foreach (var metric in result.RuleMetrics)
            {
                await AppendAsync(new TelemetryEvent
                {
                    EventType    = "rule_metric",
                    InstallId    = installId,
                    RepoHash     = repoHash,
                    RuleId       = metric.RuleId,
                    DurationMs   = metric.DurationMs,
                    Outcome      = metric.Outcome.ToString(),
                    FindingCount = metric.FindingCount,
                });
            }

            // Upload in the background only for shared mode
            if (mode == TelemetryMode.Shared)
                TelemetryUploader.UploadInBackground();
        }
        catch (Exception ex) { Console.Error.WriteLine($"[GauntletCI] Telemetry collection failed: {ex.Message}"); }
    }

    private static string? ExtractExt(string? evidence)
    {
        if (string.IsNullOrEmpty(evidence)) return null;
        // Evidence often contains a file path — extract extension only
        var parts = evidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var ext = Path.GetExtension(part.TrimEnd(':').TrimEnd(','));
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 6)
                return ext.ToLowerInvariant();
        }
        return null;
    }

    // Dual-write: JSON queue (upload buffer) + SQLite (durable local store).
    private static async Task AppendAsync(TelemetryEvent evt)
    {
        await TelemetryStore.AppendAsync(evt);
        await TelemetryDb.AppendAsync(evt);
    }
}
