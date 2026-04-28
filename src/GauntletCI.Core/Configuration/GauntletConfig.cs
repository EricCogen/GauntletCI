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

    /// <summary>Pattern consistency configuration.</summary>
    public PatternConsistencyConfig PatternConsistency { get; set; } = new();

    /// <summary>Experimental feature flags. Settings here may change or be removed without notice.</summary>
    public ExperimentalConfig Experimental { get; set; } = new();

    /// <summary>CI/CD output integration settings (GitHub PR comments, Checks API, annotations).</summary>
    public CiConfig Ci { get; set; } = new();

    /// <summary>Notification webhook settings (Slack, Teams).</summary>
    public NotificationsConfig Notifications { get; set; } = new();

    /// <summary>Default output and display settings.</summary>
    public OutputConfig Output { get; set; } = new();

    /// <summary>Ticket provider integration settings (Jira, Linear, GitHub Issues).</summary>
    public TicketProviderConfig TicketProvider { get; set; } = new();

    /// <summary>
    /// Number of context lines passed to git diff (-U flag).
    /// More context lets rules inspect surrounding code for better accuracy.
    /// Default: 10. Git default is 3.
    /// </summary>
    public int DiffContextLines { get; set; } = 10;
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
    /// Defaults to <c>~/.gauntletci/models/phi4-mini</c> when null or absent.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Name of the environment variable that holds the API key for the CI endpoint.
    /// The key is never stored in config: always read from the environment at runtime.
    /// </summary>
    public string CiApiKeyEnv { get; set; } = "GAUNTLETCI_LLM_KEY";

    /// <summary>
    /// Name of the environment variable holding the GauntletCI license key.
    /// Required to enable CI LLM enrichment.
    /// </summary>
    public string LicenseKeyEnv { get; set; } = "GAUNTLETCI_LICENSE";

    /// <summary>
    /// Ollama context window size in tokens (input + output combined).
    /// Set this to match your model's actual context window.
    /// Default: 16384 (fits phi4-mini, phi3, mistral-7b).
    /// </summary>
    public int NumCtx { get; set; } = 16_384;

    /// <summary>
    /// Maximum tokens the model may generate per completion call.
    /// Default: 2048 -- sufficient for EP policy findings and enrichment summaries.
    /// </summary>
    public int MaxCompleteTokens { get; set; } = 2_048;

    /// <summary>
    /// Ollama base URL used by <c>analyze --with-expert-context</c> for query embedding.
    /// Must match the URL used when running <c>gauntletci llm seed</c>.
    /// Default: <c>http://localhost:11434</c>.
    /// </summary>
    public string EmbeddingOllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Ollama model used for LLM enrichment and expert-context embedding.
    /// Default: <c>phi4-mini:latest</c> (pull with: <c>ollama pull phi4-mini</c>).
    /// </summary>
    public string Model { get; set; } = LlmDefaults.OllamaModel;

    /// <summary>
    /// Enable LLM enrichment of High-confidence findings by default.
    /// Equivalent to passing --with-llm on every invocation.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Attach expert context from the local vector store by default.
    /// Equivalent to passing --with-expert-context on every invocation.
    /// Requires 'gauntletci llm seed' to have been run.
    /// </summary>
    public bool ExpertContext { get; set; } = false;
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
    /// Requires an LLM to be available (local model or CI endpoint). Findings are emitted as Advisory severity : 
    /// always shown in output but never block a commit.
    /// </summary>
    public string Description { get; set; } =
        "Evaluates diffs against a structured engineering policy document using an LLM. " +
        "Requires an LLM to be available (local model or CI endpoint). Findings are emitted as Advisory severity: " +
        "shown in output but never block a commit.";

    /// <summary>
    /// Path to the engineering policy markdown file, relative to the repository root.
    /// Defaults to .misc/engineering-policy.md.
    /// </summary>
    public string Path { get; set; } = ".misc/engineering-policy.md";

    /// <summary>
    /// Maximum diff size in characters sent to the LLM. Diffs larger than this are rejected
    /// for unlicensed (Community) users. Licensed users (Business/Enterprise) are allowed through
    /// but the diff is still truncated to this limit to fit LLM context.
    /// Default: 12000 (~3000 tokens at 4 chars/token, fits in a 16K context window).
    /// </summary>
    public int MaxDiffChars { get; set; } = 12_000;
}

/// <summary>An Ollama server endpoint with an optional enabled toggle.</summary>
public class OllamaEndpoint
{
    /// <summary>Base URL of the Ollama server (e.g. "http://localhost:11434").</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Whether this endpoint is active. Disabled endpoints are skipped during round-robin. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Configuration for GCI0046 Pattern Consistency Deviation.
/// </summary>
public class PatternConsistencyConfig
{
    /// <summary>
    /// Method base names for which a sync+async pair is intentionally supported.
    /// GCI0046 will not flag a finding when both <c>Foo</c> and <c>FooAsync</c>
    /// are added in the same diff and <c>Foo</c> appears in this list.
    /// Example: ["Subscribe", "Unsubscribe", "Register", "Deregister"]
    /// </summary>
    public string[] AllowedSyncAsyncPairs { get; set; } = [];
}

/// <summary>CI/CD output integration settings.</summary>
public class CiConfig
{
    /// <summary>Post findings as a GitHub PR review with inline comments. Equivalent to --github-pr-comments.</summary>
    public bool PrComments { get; set; } = false;

    /// <summary>Post findings as a GitHub Checks API check run with annotations. Equivalent to --github-checks.</summary>
    public bool Checks { get; set; } = false;

    /// <summary>Emit GitHub Actions workflow commands for inline PR annotations. Equivalent to --github-annotations.</summary>
    public bool Annotations { get; set; } = false;

    /// <summary>Correlate findings with Codecov coverage data. Equivalent to --with-coverage.</summary>
    public bool Coverage { get; set; } = false;
}

/// <summary>Notification webhook settings.</summary>
public class NotificationsConfig
{
    /// <summary>
    /// Slack Incoming Webhook URL. Posts Block findings to Slack.
    /// Equivalent to --notify-slack. Can also be set via GAUNTLETCI_SLACK_WEBHOOK env var.
    /// </summary>
    public string? SlackWebhook { get; set; }

    /// <summary>
    /// Microsoft Teams Incoming Webhook URL. Posts Block findings to Teams.
    /// Equivalent to --notify-teams. Can also be set via GAUNTLETCI_TEAMS_WEBHOOK env var.
    /// </summary>
    public string? TeamsWebhook { get; set; }
}

/// <summary>Default output and display settings.</summary>
public class OutputConfig
{
    /// <summary>
    /// Minimum severity to display: info, warn, block. Defaults to warn.
    /// Equivalent to --severity. CLI flag overrides this value.
    /// </summary>
    public string MinSeverity { get; set; } = "warn";

    /// <summary>Enable verbose output (show Info-severity findings). Equivalent to --verbose.</summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// Confidence-based noise filter: strict, balanced (default), or permissive.
    /// Equivalent to --sensitivity. CLI flag overrides this value.
    /// </summary>
    public string Sensitivity { get; set; } = "balanced";

    /// <summary>Output format: text, json, or sarif. Defaults to text. Equivalent to --output.</summary>
    public string Format { get; set; } = "text";
}

/// <summary>Ticket provider integration settings.</summary>
public class TicketProviderConfig
{
    /// <summary>
    /// Enable ticket context enrichment. Parses branch name and GITHUB_PR_BODY for issue keys
    /// and fetches ticket details from Jira, Linear, or GitHub Issues.
    /// Equivalent to --with-ticket-context.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
