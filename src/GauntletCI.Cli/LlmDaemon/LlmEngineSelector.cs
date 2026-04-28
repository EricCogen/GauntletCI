// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Licensing;
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

        // Daemon unavailable (model not cached or spawn failed): direct load, silent
        return new LocalLlmEngine(config.Llm?.ModelPath);
    }

    private static ILlmEngine ResolveForCi(GauntletConfig config)
    {
        var llmCfg = config.Llm;
        if (llmCfg is null)
            return WarnAndSkip(
                "--with-llm was passed but no 'llm' block exists in .gauntletci.json.",
                "The built-in ONNX engine is not available in CI. A remote endpoint is required.",
                "Add an 'llm' block with 'ciEndpoint' and set the license key env var to enable enrichment in CI.");

        // License key check via LicenseService (reads env var or ~/.gauntletci/gauntletci.key)
        var license = LicenseService.Load(llmCfg.LicenseKeyEnv);
        if (!license.IsLicensed)
            return WarnAndSkip(
                $"--with-llm was passed but no valid license was found (checked ${llmCfg.LicenseKeyEnv} and ~/.gauntletci/gauntletci.key).",
                license.Error ?? "LLM enrichment in CI requires a valid GauntletCI Pro or higher license.",
                "Get a license at https://gauntletci.com/pricing and place it in ~/.gauntletci/gauntletci.key or set the env var.");

        if (string.IsNullOrWhiteSpace(llmCfg.CiEndpoint))
            return WarnAndSkip(
                "--with-llm was passed but 'llm.ciEndpoint' is not set in .gauntletci.json.",
                "The built-in ONNX engine is not available in CI. A remote OpenAI-compatible endpoint is required.",
                "Set 'llm.ciEndpoint' to your remote LLM URL (e.g. https://api.openai.com/v1/chat/completions).");

        var apiKey = Environment.GetEnvironmentVariable(llmCfg.CiApiKeyEnv);
        if (string.IsNullOrWhiteSpace(apiKey))
            return WarnAndSkip(
                $"--with-llm was passed but no API key was found in ${llmCfg.CiApiKeyEnv}.",
                "A remote LLM endpoint is configured but the API key env var is missing.",
                $"Set ${llmCfg.CiApiKeyEnv} in your pipeline secrets and retry.");

        return new RemoteLlmEngine(llmCfg.CiEndpoint, llmCfg.CiModel, apiKey,
            llmCfg.NumCtx, llmCfg.MaxCompleteTokens);
    }

    private static NullLlmEngine WarnAndSkip(string problem, string reason, string fix)
    {
        var bar = new string('-', 72);
        Console.Error.WriteLine();
        Console.Error.WriteLine($"[GauntletCI] WARNING {bar.Substring(20)}");
        Console.Error.WriteLine($"  Problem : {problem}");
        Console.Error.WriteLine($"  Reason  : {reason}");
        Console.Error.WriteLine($"  Fix     : {fix}");
        Console.Error.WriteLine($"  Result  : LLM enrichment skipped. Analysis will continue without it.");
        Console.Error.WriteLine($"[GauntletCI] {bar.Substring(13)}");
        Console.Error.WriteLine();
        return new NullLlmEngine();
    }
}
