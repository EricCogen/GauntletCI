// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text;
using System.Text.Json;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Telemetry;

public sealed class TelemetryEmitter(HttpClient httpClient)
{
    public async Task EmitAsync(EvaluationResult result, GauntletConfig config, CancellationToken cancellationToken)
    {
        EvaluationAuditTrail trail = result.AuditTrail ?? BuildFallbackTrail(result, config);
        DiffMetadata metadata = trail.AnalysisCompleted.DiffMetadata ?? EmptyDiffMetadata();
        IReadOnlyDictionary<string, int> ruleFireCounts = trail.RuleFirings
            .GroupBy(static ruleEvent => ruleEvent.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

        object payload = new
        {
            session_id = trail.EvaluationId,
            timestamp = trail.CapturedAtUtc,
            schema_version = trail.SchemaVersion,
            rules_fired = trail.RuleFirings.Select(static ruleEvent => ruleEvent.RuleId).Distinct().ToArray(),
            rule_fire_counts = ruleFireCounts,
            severities = new
            {
                high = trail.RuleFirings.Count(static ruleEvent => ruleEvent.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)),
                medium = trail.RuleFirings.Count(static ruleEvent => ruleEvent.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)),
                low = trail.RuleFirings.Count(static ruleEvent => ruleEvent.Severity.Equals("low", StringComparison.OrdinalIgnoreCase)),
            },
            gates = new
            {
                branch_currency = trail.AnalysisCompleted.BranchCurrencyPassed ? "pass" : "fail",
                test_passage = trail.AnalysisCompleted.TestPassagePassed ? "pass" : "fail",
            },
            config = new
            {
                model = trail.ConfigResolved.Model,
                model_required = trail.ConfigResolved.ModelRequired,
                telemetry_enabled = trail.ConfigResolved.TelemetryEnabled,
                policy_refs_count = trail.ConfigResolved.PolicyReferences.Count,
            },
            diff_metadata = new
            {
                lines_added = metadata.LinesAdded,
                lines_removed = metadata.LinesRemoved,
                files_changed = metadata.FilesChanged,
                test_files_touched = metadata.TestFilesTouched,
                test_files_changed = metadata.TestFilesChanged,
                test_files_with_content_changes = metadata.TestFilesWithContentChanges,
                test_files_rename_only = metadata.TestFilesRenameOnly,
                test_lines_added = metadata.TestLinesAdded,
                test_lines_removed = metadata.TestLinesRemoved,
                test_assertion_lines_added = metadata.TestAssertionLinesAdded,
                test_setup_lines_added = metadata.TestSetupLinesAdded,
                tests_changed_without_assertions = metadata.TestsChangedWithoutAssertions,
                test_changes_are_rename_or_setup_churn = metadata.TestChangesAreRenameOrSetupChurn,
                languages = metadata.Languages,
                diff_trimmed = metadata.DiffTrimmed,
            },
            action = "evaluated",
            model = trail.AnalysisCompleted.Model,
            model_step_skipped = trail.AnalysisCompleted.ModelStepSkipped,
            finding_count = trail.AnalysisCompleted.FindingCount,
            has_error = trail.AnalysisCompleted.HasError,
            evaluation_duration_ms = trail.AnalysisCompleted.EvaluationDurationMs,
        };

        string endpoint = Environment.GetEnvironmentVariable("GAUNTLETCI_TELEMETRY_ENDPOINT") ?? "https://telemetry.gauntletci.dev/v1/events";
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _ = response.IsSuccessStatusCode;
        }
        catch
        {
            // Telemetry must never block the developer workflow.
        }
    }

    private static EvaluationAuditTrail BuildFallbackTrail(EvaluationResult result, GauntletConfig config)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ConfigResolvedEvent configResolved = new(
            EventId: Guid.NewGuid().ToString("N"),
            OccurredAtUtc: now,
            ConfigFormat: ".gauntletci.json",
            Model: config.Model,
            ModelRequired: config.ModelRequired,
            TelemetryEnabled: config.Telemetry,
            TelemetryConsentRecorded: config.TelemetryConsentRecorded,
            DisabledRules: [.. config.DisabledRules],
            BlockingRules: [.. config.BlockingRules],
            PolicyReferences: [.. config.PolicyReferences]);
        AnalysisCompletedEvent analysisCompleted = new(
            EventId: Guid.NewGuid().ToString("N"),
            OccurredAtUtc: now,
            ExitCode: result.ExitCode,
            ModelStepSkipped: result.ModelStepSkipped,
            DiffTrimmed: result.DiffTrimmed,
            HasError: !string.IsNullOrWhiteSpace(result.ErrorMessage),
            EvaluationDurationMs: result.EvaluationDurationMs,
            Model: result.Model,
            BranchCurrencyPassed: result.BranchCurrencyGate?.Passed == true,
            TestPassagePassed: result.TestPassageGate?.Passed == true,
            FindingCount: result.Findings.Count,
            DiffMetadata: result.DiffMetadata);
        IReadOnlyList<RuleFiredEvent> rules = result.Findings
            .Select(finding => new RuleFiredEvent(
                EventId: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: now,
                RuleId: finding.RuleId,
                RuleName: finding.RuleName,
                Severity: finding.Severity,
                Confidence: finding.Confidence,
                Evidence: finding.Evidence))
            .ToArray();
        return new EvaluationAuditTrail(
            SchemaVersion: "1",
            EvaluationId: Guid.NewGuid(),
            CapturedAtUtc: now,
            ConfigResolved: configResolved,
            AnalysisCompleted: analysisCompleted,
            RuleFirings: rules);
    }

    private static DiffMetadata EmptyDiffMetadata()
    {
        return new DiffMetadata(
            0,
            0,
            0,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            false,
            [],
            false,
            0);
    }
}
