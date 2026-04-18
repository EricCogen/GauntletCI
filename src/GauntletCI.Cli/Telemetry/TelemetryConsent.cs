// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Cli.Telemetry;

/// <summary>Controls where telemetry events are stored and whether they are uploaded.</summary>
public enum TelemetryMode
{
    /// <summary>Telemetry is disabled; no events are written or sent.</summary>
    Off,
    /// <summary>Events are stored locally only; no network calls are made.</summary>
    Local,
    /// <summary>Events are stored locally and uploaded as anonymous aggregates.</summary>
    Shared,
}

/// <summary>
/// Reads and persists the user's telemetry consent decision in ~/.gauntletci/config.json.
/// Migrates legacy consent.json automatically on first access.
/// </summary>
public static class TelemetryConsent
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "config.json");

    private static readonly string LegacyConsentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "consent.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private record TelemetrySection(
        string InstallId,
        string? Mode,
        DateTimeOffset? ConfiguredAt);

    private record RootConfig(TelemetrySection? Telemetry);

    // Legacy record shape for migrating consent.json → config.json
    private record LegacyConsentRecord(string InstallId, bool? OptedIn, DateTimeOffset? DecidedAt);

    private static RootConfig? _cache;

    /// <summary>Stable anonymous identifier for this installation, generated once and persisted.</summary>
    public static string InstallId => LoadTelemetry().InstallId;

    /// <summary>True when the current telemetry mode is not <see cref="TelemetryMode.Off"/>.</summary>
    public static bool IsOptedIn => GetMode() != TelemetryMode.Off;

    /// <summary>True when the user has explicitly chosen a mode (Off, Local, or Shared).</summary>
    public static bool HasDecided => ParseMode(LoadTelemetry().Mode).HasValue;

    /// <summary>Returns the effective telemetry mode, defaulting to Off when undecided.</summary>
    public static TelemetryMode GetMode() => ParseMode(LoadTelemetry().Mode) ?? TelemetryMode.Off;

    /// <summary>
    /// Prompts the user interactively to choose a telemetry mode if they have not yet decided.
    /// Skips the prompt in CI environments or when stdin/stdout is redirected.
    /// </summary>
    /// <returns>True when telemetry is enabled after the prompt (or was already enabled).</returns>
    public static bool PromptIfNeeded()
    {
        if (HasDecided) return IsOptedIn;
        if (Console.IsInputRedirected || Console.IsOutputRedirected) return false;
        if (IsCI()) return false;

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  🔒 Help improve GauntletCI?                                │");
        Console.WriteLine("│                                                             │");
        Console.WriteLine("│  Share anonymous usage data to make the rules smarter.      │");
        Console.WriteLine("│  No code, file paths, or personal data ever leaves your     │");
        Console.WriteLine("│  machine. Everything is salted and hashed locally.          │");
        Console.WriteLine("│                                                             │");
        Console.WriteLine("│  • 'shared' - Store locally + send anonymous aggregates     │");
        Console.WriteLine("│  • 'local'  - Store locally only (no network calls)         │");
        Console.WriteLine("│  • 'off'    - Disable telemetry completely                  │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.Write("Choose mode [shared/local/off] (default: shared): ");

        var input = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
        var selected = input switch
        {
            "" => TelemetryMode.Shared,
            "shared" => TelemetryMode.Shared,
            "local" => TelemetryMode.Local,
            "off" => TelemetryMode.Off,
            _ => TelemetryMode.Shared,
        };

        SetMode(selected);
        Console.WriteLine($"Telemetry mode set to: {selected.ToString().ToLowerInvariant()}");
        Console.WriteLine();

        return selected != TelemetryMode.Off;
    }

    /// <summary>
    /// Convenience wrapper that maps a boolean opt-in to Shared or Off mode.
    /// </summary>
    /// <param name="value">True to enable shared telemetry; false to disable.</param>
    public static void SetOptIn(bool value)
    {
        SetMode(value ? TelemetryMode.Shared : TelemetryMode.Off);
    }

    /// <summary>
    /// Persists the chosen telemetry mode to config.json and updates the in-memory cache.
    /// </summary>
    /// <param name="mode">The mode to activate.</param>
    public static void SetMode(TelemetryMode mode)
    {
        var record = LoadTelemetry() with
        {
            Mode = mode.ToString().ToLowerInvariant(),
            ConfiguredAt = DateTimeOffset.UtcNow,
        };

        Save(record);
    }

    private static TelemetrySection LoadTelemetry()
    {
        var cfg = LoadConfig();
        if (cfg.Telemetry is not null)
            return cfg.Telemetry;

        var created = new TelemetrySection(Guid.NewGuid().ToString(), null, null);
        Save(created);
        return created;
    }

    private static RootConfig LoadConfig()
    {
        if (_cache is not null) return _cache;

        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _cache = JsonSerializer.Deserialize<RootConfig>(json, JsonOptions) ?? new RootConfig(null);
                return _cache;
            }
        }
        catch
        {
            // ignore corrupt config and continue with a fresh one
        }

        // Migrate from legacy consent.json if it exists
        if (File.Exists(LegacyConsentPath))
        {
            try
            {
                var legacyJson = File.ReadAllText(LegacyConsentPath);
                var legacy = JsonSerializer.Deserialize<LegacyConsentRecord>(legacyJson, JsonOptions);
                if (legacy is not null)
                {
                    var migratedMode = legacy.OptedIn switch
                    {
                        true  => "shared",
                        false => "off",
                        null  => null,
                    };
                    var section = new TelemetrySection(legacy.InstallId, migratedMode, legacy.DecidedAt);
                    Save(section);
                    return _cache!;
                }
            }
            catch { /* ignore corrupt legacy file */ }
        }

        _cache = new RootConfig(null);
        return _cache;
    }

    private static void Save(TelemetrySection section)
    {
        _cache = new RootConfig(section);

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, ConfigPath, overwrite: true);
    }

    private static TelemetryMode? ParseMode(string? value) => value?.ToLowerInvariant() switch
    {
        "shared" => TelemetryMode.Shared,
        "local" => TelemetryMode.Local,
        "off" => TelemetryMode.Off,
        _ => null,
    };

    private static bool IsCI() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
}
