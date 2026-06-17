// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Cli.Mcp;

/// <summary>
/// Shared analyze pipeline for MCP tools: config, ignore list, Roslyn/static analysis, orchestrator.
/// Matches the core path in <c>gauntletci analyze</c> before LLM enrichment and output formatting.
/// </summary>
internal static class McpAnalysisPipeline
{
    public static async Task<EvaluationResult> RunAsync(
        DiffContext diff,
        string? repoPath = null,
        CancellationToken ct = default)
    {
        GauntletConfig config;
        IgnoreList? ignoreList = null;

        if (!string.IsNullOrWhiteSpace(repoPath))
        {
            config = ConfigLoader.Load(repoPath);
            ignoreList = IgnoreList.Load(repoPath);
        }
        else
        {
            config = new GauntletConfig();
        }

        var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repoPath);
        var staticAnalysis = await StaticAnalysisRunner.RunAsync(diff, repoPath, ct).ConfigureAwait(false);
        return await orchestrator.RunAsync(diff, staticAnalysis, ignoreList, ct).ConfigureAwait(false);
    }
}
