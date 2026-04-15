// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Creates <see cref="ILlmLabeler"/> instances by provider name.
/// Supported providers: ollama, anthropic, github-models, none.
/// </summary>
public static class LlmLabelerFactory
{
    /// <summary>
    /// Creates an <see cref="ILlmLabeler"/> for the given provider.
    /// Returns a <see cref="NullLlmLabeler"/> for unknown or unconfigured providers.
    /// </summary>
    /// <param name="provider">Provider name: ollama | anthropic | github-models | none</param>
    /// <param name="model">Model name (provider-specific). Null uses the provider default.</param>
    /// <param name="baseUrl">Base URL override (used by ollama). Null uses the provider default.</param>
    public static ILlmLabeler Create(string provider, string? model = null, string? baseUrl = null)
    {
        return provider.ToLowerInvariant() switch
        {
            "ollama"         => CreateOllama(model, baseUrl),
            "anthropic"      => CreateAnthropic(model),
            "github-models"  => CreateGitHubModels(model),
            "github"         => CreateGitHubModels(model),
            "none" or "null" => new NullLlmLabeler(),
            _                => new NullLlmLabeler(),
        };
    }

    /// <summary>Lists the provider names understood by this factory.</summary>
    public static IReadOnlyList<string> SupportedProviders =>
        ["ollama", "anthropic", "github-models", "none"];

    // -- Provider constructors -------------------------------------------------

    private static ILlmLabeler CreateOllama(string? model, string? baseUrl)
    {
        // If no model specified, pick the best one for this machine's hardware
        var m = !string.IsNullOrWhiteSpace(model)
            ? model
            : HardwareProfile.Detect().RecommendedModel;
        var u = baseUrl ?? "http://localhost:11434";
        return new OllamaLlmLabeler(m, u);
    }

    private static ILlmLabeler CreateAnthropic(string? model)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) return new NullLlmLabeler();
        var m = model ?? "claude-haiku-4-5";
        return new AnthropicLlmLabeler(apiKey, m);
    }

    private static ILlmLabeler CreateGitHubModels(string? model)
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(token)) return new NullLlmLabeler();
        var m = model ?? "gpt-4o-mini";
        return new GitHubModelsLlmLabeler(token, m);
    }
}
