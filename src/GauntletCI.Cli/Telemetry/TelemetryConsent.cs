// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// Manages the user's telemetry consent decision.
/// Consent is stored in ~/.gauntletci/consent.json.
/// </summary>
public static class TelemetryConsent
{
    private static readonly string ConsentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "consent.json");

    private record ConsentRecord(
        string InstallId,
        bool? OptedIn,
        DateTimeOffset? DecidedAt);

    private static ConsentRecord? _cache;

    public static string InstallId => Load().InstallId;

    public static bool IsOptedIn => Load().OptedIn == true;

    public static bool HasDecided => Load().OptedIn.HasValue;

    /// <summary>
    /// If the user hasn't decided yet, shows the opt-in prompt.
    /// Safe to call in CI/redirected contexts — silently skips.
    /// Returns true if telemetry should be collected this run.
    /// </summary>
    public static bool PromptIfNeeded()
    {
        if (HasDecided) return IsOptedIn;
        if (Console.IsInputRedirected || Console.IsOutputRedirected) return false;
        if (IsCI()) return false;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  ┌──────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  Help improve GauntletCI (anonymous telemetry)       │");
        Console.WriteLine("  │                                                      │");
        Console.WriteLine("  │  GauntletCI can share anonymous usage data:          │");
        Console.WriteLine("  │  • Which rules fire and how often                    │");
        Console.WriteLine("  │  • File types analysed (e.g. .cs, .ts)              │");
        Console.WriteLine("  │  • No code, no file paths, no identifiers            │");
        Console.WriteLine("  │                                                      │");
        Console.WriteLine("  │  Run 'gauntletci telemetry --status' to opt out.    │");
        Console.WriteLine("  └──────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.Write("  Enable anonymous telemetry? [y/N] ");

        var key = Console.ReadKey(intercept: false);
        Console.WriteLine();
        Console.WriteLine();

        var optedIn = key.Key == ConsoleKey.Y;
        Save(optedIn);
        return optedIn;
    }

    public static void SetOptIn(bool value)
    {
        Save(value);
        _cache = null;
    }

    private static ConsentRecord Load()
    {
        if (_cache is not null) return _cache;

        try
        {
            if (File.Exists(ConsentPath))
            {
                var json = File.ReadAllText(ConsentPath);
                _cache = JsonSerializer.Deserialize<ConsentRecord>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? NewRecord();
                return _cache;
            }
        }
        catch { /* ignore corrupt files */ }

        _cache = NewRecord();
        return _cache;
    }

    private static void Save(bool optedIn)
    {
        var record = Load() with { OptedIn = optedIn, DecidedAt = DateTimeOffset.UtcNow };
        _cache = record;

        Directory.CreateDirectory(Path.GetDirectoryName(ConsentPath)!);
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConsentPath, json);
    }

    private static ConsentRecord NewRecord()
    {
        var record = new ConsentRecord(Guid.NewGuid().ToString(), null, null);
        // Persist the install ID even before consent decision
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConsentPath)!);
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConsentPath, json);
        }
        catch { /* non-fatal */ }
        return record;
    }

    private static bool IsCI() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
}
