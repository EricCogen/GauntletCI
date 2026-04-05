using System.Text;
using System.Text.Json;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Telemetry;

public sealed class TelemetryEmitter
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    public async Task EmitAsync(EvaluationResult result, GauntletConfig config, CancellationToken cancellationToken)
    {
        DiffMetadata metadata = result.DiffMetadata ?? new DiffMetadata(0, 0, 0, false, [], false, 0);
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
                languages = metadata.Languages,
                diff_trimmed = metadata.DiffTrimmed,
            },
            model = result.Model,
            action = "evaluated",
            time_to_action_seconds = 0,
            evaluation_duration_ms = result.EvaluationDurationMs,
        };

        string endpoint = Environment.GetEnvironmentVariable("GAUNTLETCI_TELEMETRY_ENDPOINT") ?? "https://telemetry.gauntletci.dev/v1/events";
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _ = response.IsSuccessStatusCode;
        }
        catch
        {
            // Telemetry must never block the developer workflow.
        }
    }
}
