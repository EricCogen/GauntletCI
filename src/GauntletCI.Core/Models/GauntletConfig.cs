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

    [JsonPropertyName("api_key_env")]
    public string ApiKeyEnv { get; init; } = "";

    [JsonPropertyName("default_mode")]
    public string DefaultMode { get; init; } = "staged";

    [JsonPropertyName("telemetry_consent_recorded")]
    public bool TelemetryConsentRecorded { get; init; }

    public bool ShouldEmitTelemetry(bool noTelemetryFlag)
    {
        if (noTelemetryFlag)
        {
            return false;
        }

        return TelemetryConsentRecorded && Telemetry;
    }
}
