// SPDX-License-Identifier: Elastic-2.0
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Cli.Audit;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using ModelContextProtocol.Server;

namespace GauntletCI.Cli.Mcp;

[McpServerToolType]
public static class GauntletTools
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool, Description("Analyze staged changes in a git repository for pre-commit risk findings")]
    public static async Task<string> analyze_staged(
        [Description("Absolute path to git repository root. Defaults to current directory.")] string? repo = null)
    {
        var repoPath = repo ?? Directory.GetCurrentDirectory();
        try
        {
            var diff = await DiffParser.FromStagedAsync(repoPath);
            var result = await RuleOrchestrator.CreateDefault().RunAsync(diff);
            return SerializeFindings(result);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }

    [McpServerTool, Description("Analyze a raw unified diff string for pre-commit risk findings")]
    public static async Task<string> analyze_diff(
        [Description("Raw unified diff content")] string diff)
    {
        try
        {
            var diffContext = DiffParser.Parse(diff);
            var result = await RuleOrchestrator.CreateDefault().RunAsync(diffContext);
            return SerializeFindings(result);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }

    [McpServerTool, Description("Analyze a specific git commit for pre-commit risk findings")]
    public static async Task<string> analyze_commit(
        [Description("Absolute path to git repository root")] string repo,
        [Description("Commit SHA to analyze")] string commit)
    {
        try
        {
            var diff = await DiffParser.FromGitAsync(repo, commit);
            var result = await RuleOrchestrator.CreateDefault().RunAsync(diff);
            return SerializeFindings(result);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }

    [McpServerTool, Description("List all available GauntletCI analysis rules")]
    public static string list_rules()
    {
        try
        {
            var orchestrator = RuleOrchestrator.CreateDefault();
            var rulesField = typeof(RuleOrchestrator).GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance);
            var rules = (IReadOnlyList<IRule>?)rulesField?.GetValue(orchestrator) ?? [];
            var ruleList = rules.Select(r => new { id = r.Id, name = r.Name, description = r.Name }).ToList();
            return JsonSerializer.Serialize(ruleList, JsonOpts);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }

    [McpServerTool, Description("Get aggregate statistics from the GauntletCI local audit log")]
    public static async Task<string> audit_stats()
    {
        try
        {
            var entries = await AuditLog.LoadAllAsync();
            var totalScans = entries.Count;
            var scansWithFindings = entries.Count(e => e.FindingCount > 0);
            var totalFindings = entries.Sum(e => e.FindingCount);
            var topRules = entries
                .SelectMany(e => e.Findings)
                .GroupBy(f => f.RuleId)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { ruleId = g.Key, count = g.Count() })
                .ToList();
            return JsonSerializer.Serialize(new { totalScans, scansWithFindings, totalFindings, topRules }, JsonOpts);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }

    private static string SerializeFindings(EvaluationResult result)
    {
        var response = new
        {
            hasFindings = result.HasFindings,
            findingCount = result.Findings.Count,
            findings = result.Findings.Select(f => new
            {
                ruleId = f.RuleId,
                ruleName = f.RuleName,
                summary = f.Summary,
                evidence = f.Evidence,
                confidence = f.Confidence.ToString(),
                filePath = f.FilePath,
                line = f.Line,
            }).ToList(),
        };
        return JsonSerializer.Serialize(response, JsonOpts);
    }
}
