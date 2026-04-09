// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// Persists telemetry events to a local JSON queue file.
/// Events accumulate until the uploader picks them up and marks them sent.
/// Path: ~/.gauntletci/telemetry-queue.json
/// </summary>
public static class TelemetryStore
{
    private static readonly string QueuePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "telemetry-queue.json");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = false, PropertyNameCaseInsensitive = true };

    public static async Task AppendAsync(TelemetryEvent evt)
    {
        try
        {
            var events = await LoadAsync();
            events.Add(evt);
            // Keep queue bounded — drop oldest sent events first, then oldest unsent
            if (events.Count > 500)
                events = events.OrderBy(e => e.Sent).ThenBy(e => e.Timestamp).Skip(50).ToList();
            await SaveAsync(events);
        }
        catch { /* telemetry must never crash the tool */ }
    }

    public static async Task<List<TelemetryEvent>> GetPendingAsync(int limit = 100)
    {
        try
        {
            var events = await LoadAsync();
            return events.Where(e => !e.Sent).Take(limit).ToList();
        }
        catch { return []; }
    }

    public static async Task MarkSentAsync(IEnumerable<string> eventIds)
    {
        try
        {
            var ids = eventIds.ToHashSet();
            var events = await LoadAsync();
            var updated = events.Select(e => ids.Contains(e.EventId) ? e with { Sent = true } : e).ToList();
            // Purge events that have been sent for >7 days
            var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
            updated = updated.Where(e => !e.Sent || e.Timestamp > cutoff).ToList();
            await SaveAsync(updated);
        }
        catch { /* non-fatal */ }
    }

    private static async Task<List<TelemetryEvent>> LoadAsync()
    {
        if (!File.Exists(QueuePath)) return [];
        var json = await File.ReadAllTextAsync(QueuePath);
        return JsonSerializer.Deserialize<List<TelemetryEvent>>(json, JsonOpts) ?? [];
    }

    private static async Task SaveAsync(List<TelemetryEvent> events)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
        var json = JsonSerializer.Serialize(events, JsonOpts);
        // Write atomically via temp file to avoid corruption
        var tmp = QueuePath + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, QueuePath, overwrite: true);
    }
}
