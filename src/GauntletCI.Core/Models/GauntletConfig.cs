using System.Text.Json;
using System.Text.Json.Serialization;

namespace GauntletCI.Core.Models;

public sealed record GauntletConfig
{
    [JsonPropertyName("test_command")]
    public string TestCommand { get; init; } = "dotnet test";

    [JsonPropertyName("disabled_rules")]
    public IReadOnlyList<string> DisabledRules { get; init; } = [];

    [JsonPropertyName("blocking_rules")]
    public IReadOnlyList<string> BlockingRules { get; init; } = [];

    [JsonPropertyName("telemetry")]
    public bool Telemetry { get; init; } = true;

    [JsonPropertyName("model")]
    public string Model { get; init; } = "claude-sonnet-4-5";

    public static GauntletConfig Load(string repoRoot)
    {
        string repoConfigPath = Path.Combine(repoRoot, ".gauntletci.json");
        if (!File.Exists(repoConfigPath))
        {
            return new GauntletConfig();
        }

        string content = File.ReadAllText(repoConfigPath);
        GauntletConfig? config = JsonSerializer.Deserialize<GauntletConfig>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        return config ?? new GauntletConfig();
    }
}
