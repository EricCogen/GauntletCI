// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Configuration;

public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string UserConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gauntletci");

    public string UserConfigPath => Path.Combine(UserConfigDirectory, "config.json");

    public GauntletConfig LoadEffective(string repoRoot)
    {
        UserConfigFile user = LoadUserConfig();
        RepoConfigFile repo = LoadRepoConfig(repoRoot);

        bool consentRecorded = user.Telemetry is not null;
        bool telemetry = repo.Telemetry ?? user.Telemetry ?? false;

        return new GauntletConfig
        {
            TestCommand = repo.TestCommand ?? "dotnet test",
            DisabledRules = repo.DisabledRules ?? [],
            BlockingRules = repo.BlockingRules ?? [],
            Telemetry = telemetry,
            TelemetryConsentRecorded = consentRecorded,
            Model = repo.Model ?? user.Model ?? "claude-sonnet-4-6",
            ApiKeyEnv = user.ApiKeyEnv ?? "",
            DefaultMode = user.DefaultMode ?? "staged",
            BaseUrl = user.BaseUrl ?? "",
        };
    }

    public bool HasTelemetryConsentRecorded()
    {
        return LoadUserConfig().Telemetry is not null;
    }

    public bool? GetTelemetryConsent()
    {
        return LoadUserConfig().Telemetry;
    }

    public void SaveTelemetryConsent(bool enabled)
    {
        UserConfigFile user = LoadUserConfig();
        UserConfigFile updated = user with { Telemetry = enabled };
        SaveUserConfig(updated);
    }

    public void SaveUserConfig(UserConfigFile config)
    {
        Directory.CreateDirectory(UserConfigDirectory);
        string content = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(UserConfigPath, content);
    }

    public UserConfigFile LoadUserConfig()
    {
        if (!File.Exists(UserConfigPath))
        {
            return new UserConfigFile();
        }

        string content = File.ReadAllText(UserConfigPath);
        UserConfigFile? config = JsonSerializer.Deserialize<UserConfigFile>(content, JsonOptions);
        return config ?? new UserConfigFile();
    }

    private static RepoConfigFile LoadRepoConfig(string repoRoot)
    {
        string repoConfigPath = Path.Combine(repoRoot, ".gauntletci.json");
        if (!File.Exists(repoConfigPath))
        {
            return new RepoConfigFile();
        }

        string content = File.ReadAllText(repoConfigPath);
        try
        {
            RepoConfigFile? config = JsonSerializer.Deserialize<RepoConfigFile>(content, JsonOptions);
            return config ?? new RepoConfigFile();
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Warning: .gauntletci.json is malformed and will be ignored: {ex.Message}");
            return new RepoConfigFile();
        }
    }

    public sealed record UserConfigFile(
        [property: JsonPropertyName("telemetry")] bool? Telemetry = null,
        [property: JsonPropertyName("model")] string? Model = null,
        [property: JsonPropertyName("api_key_env")] string? ApiKeyEnv = "ANTHROPIC_API_KEY",
        [property: JsonPropertyName("default_mode")] string? DefaultMode = "staged",
        [property: JsonPropertyName("base_url")] string? BaseUrl = null);

    private sealed record RepoConfigFile(
        [property: JsonPropertyName("test_command")] string? TestCommand = null,
        [property: JsonPropertyName("disabled_rules")] IReadOnlyList<string>? DisabledRules = null,
        [property: JsonPropertyName("blocking_rules")] IReadOnlyList<string>? BlockingRules = null,
        [property: JsonPropertyName("telemetry")] bool? Telemetry = null,
        [property: JsonPropertyName("model")] string? Model = null);
}
