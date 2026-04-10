// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Represents the .gauntletci.json configuration file at the repo root.
/// </summary>
public class GauntletConfig
{
    /// <summary>Per-rule configuration keyed by rule ID (e.g. "GCI0002").</summary>
    public Dictionary<string, RuleConfig> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
