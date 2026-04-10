// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// Persists telemetry events to a local JSON queue file.
/// Events accumulate until the uploader picks them up and marks them sent.
/// Path: ~/.gauntletci/telemetry-queue.json
///
/// Concurrency strategy (two layers):
///   1. SemaphoreSlim (_inProcessGuard) — prevents concurrent async callers within
///      the same process from all entering Mutex.WaitOne() simultaneously.
///   2. Named Mutex (cross-process) — serializes access across multiple CLI processes
///      that share the same queue file (e.g., parallel pre-commit hooks).
/// All file I/O inside the mutex is synchronous to avoid thread-affinity violations.
/// Unique per-write temp file names prevent cross-process .tmp collisions.
/// </summary>
public static class TelemetryStore
{
    private static readonly string QueuePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "telemetry-queue.json");

    // Cross-process named mutex — scoped to current user session on all platforms.
    private const string MutexName = "GauntletCI.TelemetryQueue";

    // In-process guard: keeps concurrent async callers from all blocking on WaitOne at once.
    private static readonly SemaphoreSlim _inProcessGuard = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = false, PropertyNameCaseInsensitive = true };

    public static async Task AppendAsync(TelemetryEvent evt)
    {
        await _inProcessGuard.WaitAsync().ConfigureAwait(false);
        try
        {
            WithMutex(() =>
            {
                var events = Load();
                events.Add(evt);
                // Keep queue bounded — drop oldest sent events first, then oldest unsent
                if (events.Count > 500)
                    events = events.OrderBy(e => e.Sent).ThenBy(e => e.Timestamp).Skip(50).ToList();
                Save(events);
            });
        }
        catch { /* telemetry must never crash the tool */ }
        finally { _inProcessGuard.Release(); }
    }

    public static async Task<List<TelemetryEvent>> GetPendingAsync(int limit = 100)
    {
        await _inProcessGuard.WaitAsync().ConfigureAwait(false);
        try
        {
            List<TelemetryEvent> result = [];
            WithMutex(() => result = Load().Where(e => !e.Sent).Take(limit).ToList());
            return result;
        }
        catch { return []; }
        finally { _inProcessGuard.Release(); }
    }

    public static async Task MarkSentAsync(IEnumerable<string> eventIds)
    {
        await _inProcessGuard.WaitAsync().ConfigureAwait(false);
        try
        {
            var ids = eventIds.ToHashSet();
            WithMutex(() =>
            {
                var events = Load();
                var updated = events.Select(e => ids.Contains(e.EventId) ? e with { Sent = true } : e).ToList();
                // Purge events that have been sent for >7 days
                var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
                updated = updated.Where(e => !e.Sent || e.Timestamp > cutoff).ToList();
                Save(updated);
            });
        }
        catch { /* non-fatal */ }
        finally { _inProcessGuard.Release(); }
    }

    private static void WithMutex(Action action)
    {
        using var mutex = new Mutex(false, MutexName);
        var acquired = false;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.FromSeconds(10));
            action();
        }
        finally
        {
            if (acquired)
            {
                try { mutex.ReleaseMutex(); }
                catch (ApplicationException) { /* mutex was abandoned by another process */ }
            }
        }
    }

    private static List<TelemetryEvent> Load()
    {
        if (!File.Exists(QueuePath)) return [];
        var json = File.ReadAllText(QueuePath);
        return JsonSerializer.Deserialize<List<TelemetryEvent>>(json, JsonOpts) ?? [];
    }

    private static void Save(List<TelemetryEvent> events)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
        var json = JsonSerializer.Serialize(events, JsonOpts);
        // Unique temp name per write prevents cross-process .tmp file collisions
        var tmp = QueuePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, QueuePath, overwrite: true);
    }
}

