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
        DiffMetadata metadata = result.DiffMetadata ?? new DiffMetadata(
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
        object payload = new
        {
            session_id = Guid.NewGuid(),
            timestamp = DateTimeOffset.UtcNow,
            schema_version = "1",
            rules_fired = result.Findings.Select(static finding => finding.RuleId).Distinct().ToArray(),
            severities = new
            {
                high = result.Findings.Count(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)),
                medium = result.Findings.Count(static finding => finding.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)),
                low = result.Findings.Count(static finding => finding.Severity.Equals("low", StringComparison.OrdinalIgnoreCase)),
            },
            gates = new
            {
                branch_currency = result.BranchCurrencyGate?.Passed == true ? "pass" : "fail",
                test_passage = result.TestPassageGate?.Passed == true ? "pass" : "fail",
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
            model = result.Model,
            action = "evaluated",
            evaluation_duration_ms = result.EvaluationDurationMs,
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
}
