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
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "audit-log.ndjson");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly SemaphoreSlim _guard = new(1, 1);

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
        catch { /* audit log must never crash the tool */ }
        finally { _guard.Release(); }
    }

    public static async Task<IReadOnlyList<AuditLogEntry>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LogPath))
            return [];

        var lines = await File.ReadAllLinesAsync(LogPath, ct).ConfigureAwait(false);
        var entries = new List<AuditLogEntry>(lines.Length);
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
