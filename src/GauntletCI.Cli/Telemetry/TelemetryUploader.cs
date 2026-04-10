// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Json;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// Uploads pending telemetry events to the GauntletCI telemetry endpoint.
/// Upload failures are silent — never block or crash the tool.
/// </summary>
public static class TelemetryUploader
{
    private const string Endpoint = "https://telemetry.gauntletci.dev/v1/batch";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Fire-and-forget: upload pending events in the background.
    /// Call without await from the CLI to avoid blocking.
    /// </summary>
    public static void UploadInBackground() =>
        Task.Run(UploadAsync).ContinueWith(_ => { }); // swallow all exceptions

    public static async Task UploadAsync()
    {
        try
        {
            if (TelemetryConsent.GetMode() != TelemetryMode.Shared) return;

            var pending = await TelemetryStore.GetPendingAsync();
            if (pending.Count == 0) return;

            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.Add("X-GauntletCI-Version", "2.0.0");

            var payload = new { events = pending };
            var response = await http.PostAsJsonAsync(Endpoint, payload);

            if (response.IsSuccessStatusCode)
                await TelemetryStore.MarkSentAsync(pending.Select(e => e.EventId));
        }
        catch { /* upload failures are always silent */ }
    }
}
