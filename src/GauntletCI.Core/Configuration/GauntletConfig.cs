// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Represents the .gauntletci.json configuration file at the repo root.
/// </summary>
public class GauntletConfig
{
    /// <summary>Per-rule configuration keyed by rule ID (e.g. "GCI0002").</summary>
    public Dictionary<string, RuleConfig> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Controls which severity level causes a non-zero exit code.
    /// Valid values: <c>"Block"</c> (default) or <c>"Warn"</c>.
    /// </summary>
    public string ExitOn { get; set; } = "Block";

    /// <summary>Paths to external policy files to merge.</summary>
    public string[] PolicyReferences { get; set; } = [];

    /// <summary>Premium LLM configuration for CI/CD enrichment.</summary>
    public LlmConfig? Llm { get; set; }

    /// <summary>
    /// Per-layer forbidden import rules for GCI0035 Architecture Layer Guard.
    /// Key: a namespace fragment identifying the source layer (e.g. "Domain").
    /// Value: list of namespace fragments that the source layer must not import (e.g. ["Infrastructure", "AspNetCore"]).
    /// </summary>
    public Dictionary<string, List<string>>? ForbiddenImports { get; set; }

    /// <summary>Corpus pipeline configuration (local dev tool settings).</summary>
    public CorpusConfig Corpus { get; set; } = new();
}

/// <summary>Per-rule configuration overrides.</summary>
public class RuleConfig
{
    /// <summary>Whether the rule is enabled. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Override the rule's default severity. Valid values: "High", "Medium", "Low".
    /// Null means use the rule's default.
    /// </summary>
    public string? Severity { get; set; }
}

/// <summary>
/// Premium CI/CD LLM configuration. When present in a CI environment alongside a valid
/// license key, GauntletCI routes LLM enrichment to the user-supplied endpoint instead
/// of the local ONNX model. The endpoint must be OpenAI-chat-completions compatible.
/// </summary>
public class LlmConfig
{
    /// <summary>
    /// OpenAI-compatible chat completions endpoint.
    /// E.g. "https://api.openai.com/v1/chat/completions" or an Azure OpenAI endpoint.
    /// </summary>
    public string? CiEndpoint { get; set; }

    /// <summary>Model name to request (e.g. "gpt-4o-mini", "gpt-4o").</summary>
    public string CiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Path to the local ONNX model directory used by <c>LocalLlmEngine</c>.
    /// Defaults to <c>~/.gauntletci/models/phi3-mini</c> when null or absent.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Name of the environment variable that holds the API key for the CI endpoint.
    /// The key is never stored in config — always read from the environment at runtime.
    /// </summary>
    public string CiApiKeyEnv { get; set; } = "GAUNTLETCI_LLM_KEY";

    /// <summary>
    /// Name of the environment variable holding the GauntletCI license key.
    /// Required to enable CI LLM enrichment.
    /// </summary>
    public string LicenseKeyEnv { get; set; } = "GAUNTLETCI_LICENSE";
}

/// <summary>
/// Corpus pipeline configuration. Controls local Ollama endpoints used during silver labeling.
/// These settings are local to the developer's machine and should not be committed to source control.
/// </summary>
public class CorpusConfig
{
    /// <summary>
    /// Ollama endpoints for silver labeling. Multiple entries enable round-robin load distribution.
    /// Set <c>enabled: false</c> on an entry to disable it without removing it from config.
    /// Example: [{ "url": "http://localhost:11434" }, { "url": "http://192.168.1.5:11434", "enabled": false }]
    /// </summary>
    public OllamaEndpoint[] OllamaEndpoints { get; set; } = [];

    /// <summary>
    /// Default Ollama model override for the corpus pipeline. Null means auto-select based on hardware.
    /// Example: "phi3:mini"
    /// </summary>
    public string? OllamaModel { get; set; }
}

/// <summary>An Ollama server endpoint with an optional enabled toggle.</summary>
public class OllamaEndpoint
{
    /// <summary>Base URL of the Ollama server (e.g. "http://localhost:11434").</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Whether this endpoint is active. Disabled endpoints are skipped during round-robin. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;
}
