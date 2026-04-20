// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Llm;

namespace GauntletCI.Cli.LlmDaemon;

/// <summary>
/// Selects the appropriate ILlmEngine based on environment (CI vs local) and config.
/// </summary>
internal static class LlmEngineSelector
{
    private static readonly string[] CiEnvVars =
        ["CI", "GITHUB_ACTIONS", "TF_BUILD", "BUILD_BUILDID", "JENKINS_URL"];

    internal static bool IsRunningInCi() =>
        CiEnvVars.Any(v => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v)));

    /// <summary>
    /// Resolves and returns the LLM engine to use.
    ///
    /// CI:    RemoteLlmEngine if license key + endpoint configured, else NullLlmEngine.
    /// Local: LlmDaemonClient (auto-spawns daemon) if model cached, else NullLlmEngine.
    ///        Falls back to direct LocalLlmEngine if daemon spawn fails.
    /// </summary>
    internal static async Task<ILlmEngine> ResolveAsync(
        GauntletConfig config,
        bool withLlm,
        CancellationToken ct = default)
    {
        if (!withLlm)
            return new NullLlmEngine();

        if (IsRunningInCi())
            return ResolveForCi(config);

        // Local dev with Ollama configured -- prefer corpus.ollamaEndpoints[0] over ONNX daemon
        var ollamaUrl = config.Corpus.OllamaEndpoints.FirstOrDefault(e => e.Enabled)?.Url;
        if (!string.IsNullOrWhiteSpace(ollamaUrl))
        {
            var model    = config.Llm?.Model ?? LlmDefaults.OllamaModel;
            var endpoint = ollamaUrl.TrimEnd('/') + "/v1/chat/completions";
            var numCtx   = config.Llm?.NumCtx ?? 16_384;
            var maxTok   = config.Llm?.MaxCompleteTokens ?? 2_048;
            return new RemoteLlmEngine(endpoint, model, apiKey: "ollama", numCtx, maxTok);
        }

        // Local dev: try daemon first, then fall back to direct load
        var daemon = await LlmDaemonClient.ConnectOrStartAsync(ct);
        if (daemon is not null)
            return daemon;

        // Daemon unavailable (model not cached or spawn failed) — direct load, silent
        return new LocalLlmEngine(config.Llm?.ModelPath);
    }

    private static ILlmEngine ResolveForCi(GauntletConfig config)
    {
        var llmCfg = config.Llm;
        if (llmCfg is null)
            return new NullLlmEngine();

        // License key check (stub — non-empty env var is sufficient for now)
        var licenseKey = Environment.GetEnvironmentVariable(llmCfg.LicenseKeyEnv);
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            Console.Error.WriteLine(
                $"[GauntletCI] LLM enrichment in CI requires a license key in " +
                $"${llmCfg.LicenseKeyEnv}. Skipping enrichment.");
            return new NullLlmEngine();
        }

        if (string.IsNullOrWhiteSpace(llmCfg.CiEndpoint))
        {
            Console.Error.WriteLine(
                "[GauntletCI] llm.ci_endpoint not set in .gauntletci.json. Skipping enrichment.");
            return new NullLlmEngine();
        }

        var apiKey = Environment.GetEnvironmentVariable(llmCfg.CiApiKeyEnv);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine(
                $"[GauntletCI] LLM API key not found in ${llmCfg.CiApiKeyEnv}. Skipping enrichment.");
            return new NullLlmEngine();
        }

        return new RemoteLlmEngine(llmCfg.CiEndpoint, llmCfg.CiModel, apiKey,
            llmCfg.NumCtx, llmCfg.MaxCompleteTokens);
    }
}
