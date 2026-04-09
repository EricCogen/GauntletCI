// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Runs all registered rules against a <see cref="DiffContext"/> and aggregates findings.
/// Rules are loaded via reflection — drop a new IRule implementation into the assembly
/// and it will be picked up automatically.
/// </summary>
public class RuleOrchestrator
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly GauntletConfig _config;

    public RuleOrchestrator(IEnumerable<IRule> rules, GauntletConfig? config = null)
    {
        _rules = [.. rules.OrderBy(r => r.Id)];
        _config = config ?? new GauntletConfig();
    }

    /// <summary>Creates an orchestrator with all built-in rules auto-discovered via reflection.</summary>
    public static RuleOrchestrator CreateDefault(GauntletConfig? config = null)
    {
        config ??= new GauntletConfig();
        var ruleType = typeof(IRule);
        var rules = typeof(RuleOrchestrator).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && ruleType.IsAssignableFrom(t))
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .Where(r => IsRuleEnabled(r.Id, config))
            .ToList();
        return new RuleOrchestrator(rules, config);
    }

    public async Task<EvaluationResult> RunAsync(
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        IgnoreList? ignoreList = null,
        CancellationToken ct = default)
    {
        var allFindings = new List<Finding>();

        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var findings = await rule.EvaluateAsync(diff, staticAnalysis, ct);
                allFindings.AddRange(findings);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Rule {rule.Id} threw an exception: {ex.Message}");
            }
        }

        ApplySeverityOverrides(allFindings, _config);
        ApplyIgnoreList(allFindings, ignoreList);
        PostProcess(diff, allFindings);

        return new EvaluationResult
        {
            CommitSha = diff.CommitSha,
            Findings = allFindings,
            RulesEvaluated = _rules.Count
        };
    }

    private static bool IsRuleEnabled(string ruleId, GauntletConfig config)
    {
        if (config.Rules.TryGetValue(ruleId, out var rc))
            return rc.Enabled;
        return true;
    }

    private static void ApplySeverityOverrides(List<Finding> findings, GauntletConfig config)
    {
        foreach (var finding in findings)
        {
            if (!config.Rules.TryGetValue(finding.RuleId, out var rc)) continue;
            if (rc.Severity is null) continue;

            finding.Confidence = rc.Severity.ToUpperInvariant() switch
            {
                "HIGH"   => Confidence.High,
                "MEDIUM" => Confidence.Medium,
                "LOW"    => Confidence.Low,
                _        => finding.Confidence
            };
        }
    }

    private static void ApplyIgnoreList(List<Finding> findings, IgnoreList? ignoreList)
    {
        if (ignoreList is null || ignoreList.IsEmpty) return;
        findings.RemoveAll(f => ignoreList.IsSuppressed(f.RuleId));
    }

    /// <summary>
    /// Runs synthesis checks after all rules have completed.
    /// Adds GCI0018 aggregate finding and GCI0019 large-diff warning.
    /// </summary>
    private static void PostProcess(DiffContext diff, List<Finding> allFindings)
    {
        try
        {
            int distinctRulesFired = allFindings.Select(f => f.RuleId).Distinct().Count();
            if (distinctRulesFired > 3)
            {
                allFindings.Add(new Finding
                {
                    RuleId = "GCI0018",
                    RuleName = "Production Readiness",
                    Summary = $"{distinctRulesFired} rules flagged issues — this diff has multiple risk dimensions.",
                    Evidence = $"Rules fired: {string.Join(", ", allFindings.Select(f => f.RuleId).Distinct().OrderBy(id => id))}",
                    WhyItMatters = "Multiple concurrent risks compound each other and increase the chance of production incidents.",
                    SuggestedAction = "Address High-confidence findings first, then revisit Medium/Low before merging.",
                    Confidence = Confidence.Medium,
                });
            }

            var gci0019 = new GCI0019_ConfidenceAndEvidence();
            int totalLines = diff.AllAddedLines.Count() + diff.AllRemovedLines.Count();
            var warning = gci0019.CreateLargeDiffWarning(totalLines, allFindings.Count);
            if (warning != null) allFindings.Add(warning);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] PostProcess threw an exception: {ex.Message}");
        }
    }
}

public class EvaluationResult
{
    public string CommitSha { get; init; } = string.Empty;
    public List<Finding> Findings { get; init; } = [];
    public int RulesEvaluated { get; init; }
    public bool HasFindings => Findings.Count > 0;
}
