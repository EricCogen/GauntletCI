// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Cli.Audit;

/// <summary>
/// Appends and reads audit log entries from ~/.gauntletci/audit-log.ndjson.
/// Each line is a complete JSON-serialised <see cref="AuditLogEntry"/>.
/// NDJSON allows cheap appends without loading the entire file.
/// </summary>
public static class AuditLog
{
    /// <summary>Absolute path to the NDJSON audit log file in the user profile directory.</summary>
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "audit-log.ndjson");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly SemaphoreSlim _guard = new(1, 1);

    /// <summary>
    /// Appends a single audit entry to the log file, creating it if absent.
    /// Uses a semaphore to prevent concurrent writes from corrupting the NDJSON stream.
    /// </summary>
    /// <param name="entry">The audit entry to serialise and append.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        await _guard.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var line = JsonSerializer.Serialize(entry, JsonOpts);
            await File.AppendAllTextAsync(LogPath, line + Environment.NewLine, ct)
                      .ConfigureAwait(false);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[GauntletCI] Audit log write failed: {ex.Message}"); }
        finally { _guard.Release(); }
    }

    /// <summary>
    /// Reads and deserialises all valid entries from the audit log, skipping malformed lines.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All successfully parsed audit entries, oldest first.</returns>
    public static async Task<IReadOnlyList<AuditLogEntry>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LogPath))
            return [];

        var lines = new List<string>();
        using var reader = new StreamReader(LogPath);
        string? readLine;
        while ((readLine = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
            lines.Add(readLine);
        var entries = new List<AuditLogEntry>(lines.Count);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, JsonOpts);
                if (entry is not null) entries.Add(entry);
            }
            catch { /* skip malformed lines */ }
        }
        return entries;
    }
}
