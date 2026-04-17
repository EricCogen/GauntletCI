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

    /// <summary>Experimental feature flags. Settings here may change or be removed without notice.</summary>
    public ExperimentalConfig Experimental { get; set; } = new();
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

/// <summary>
/// Corpus pipeline configuration. Controls local Ollama endpoints used during silver labeling.
/// These settings are local to the developer's machine and should not be committed to source control.
/// </summary>
public class CorpusConfig
{
    /// <summary>
    /// Ollama base URLs for silver labeling. Multiple URLs enable round-robin load distribution
    /// across several local or remote Ollama servers.
    /// Example: ["http://localhost:11434", "http://192.168.1.5:11434"]
    /// </summary>
    public string[] OllamaUrls { get; set; } = [];

    /// <summary>
    /// Default Ollama model override for the corpus pipeline. Null means auto-select based on hardware.
    /// Example: "phi3:mini"
    /// </summary>
    public string? OllamaModel { get; set; }
}

/// <summary>Experimental features. Settings here may change or be removed without notice.</summary>
public class ExperimentalConfig
{
    /// <summary>LLM-powered engineering policy evaluation step.</summary>
    public EngineeringPolicyConfig EngineeringPolicy { get; set; } = new();
}

/// <summary>
/// Configuration for the experimental LLM-powered engineering policy evaluation step.
/// When enabled, GauntletCI sends the diff and a structured policy document to the configured
/// LLM and emits Advisory-severity findings for any detected policy violations.
/// </summary>
public class EngineeringPolicyConfig
{
    /// <summary>Enable the engineering policy evaluation step. Requires an LLM to be available.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Self-documenting description of this feature.
    /// Evaluates diffs against a structured engineering policy document using an LLM.
    /// Requires a configured LLM endpoint. Findings are emitted as Advisory severity —
    /// always shown in output but never block a commit.
    /// </summary>
    public string Description { get; set; } =
        "Evaluates diffs against a structured engineering policy document using an LLM. " +
        "Requires a configured LLM endpoint. Findings are emitted as Advisory severity — " +
        "shown in output but never block a commit.";

    /// <summary>
    /// Path to the engineering policy markdown file, relative to the repository root.
    /// Defaults to .misc/engineering-policy.md.
    /// </summary>
    public string Path { get; set; } = ".misc/engineering-policy.md";
}
