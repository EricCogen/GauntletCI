// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Runs all registered rules against a <see cref="DiffContext"/> and aggregates findings.
/// File eligibility filtering runs once before all rules via <see cref="ChangedFileAnalyzer"/>.
/// Rules are loaded via reflection — drop a new IRule implementation into the assembly
/// and it will be picked up automatically.
/// </summary>
public class RuleOrchestrator
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly GauntletConfig _config;
    private readonly TimeSpan _ruleTimeout;
    private readonly IChangedFileAnalyzer _fileAnalyzer;

    /// <summary>
    /// Initializes the orchestrator with an explicit set of rules and optional configuration.
    /// </summary>
    /// <param name="rules">The rules to evaluate, ordered by ID.</param>
    /// <param name="config">GauntletCI configuration; defaults to built-in settings when null.</param>
    /// <param name="ruleTimeout">Per-rule evaluation time limit; defaults to 30 seconds when null.</param>
    /// <param name="fileAnalyzer">File eligibility classifier; defaults to <see cref="ChangedFileAnalyzer"/> when null.</param>
    public RuleOrchestrator(IEnumerable<IRule> rules, GauntletConfig? config = null, TimeSpan? ruleTimeout = null, IChangedFileAnalyzer? fileAnalyzer = null)
    {
        _rules = [.. rules.OrderBy(r => r.Id)];
        _config = config ?? new GauntletConfig();
        _ruleTimeout = ruleTimeout ?? TimeSpan.FromSeconds(30);
        _fileAnalyzer = fileAnalyzer ?? new ChangedFileAnalyzer();
    }

    /// <summary>Creates an orchestrator with all built-in rules auto-discovered via reflection.</summary>
    public static RuleOrchestrator CreateDefault(GauntletConfig? config = null, TimeSpan? ruleTimeout = null)
    {
        config ??= new GauntletConfig();
        var ruleType = typeof(IRule);
        // Discover all IRule implementations via reflection at startup;
        // no manual registration needed — drop a new class in the assembly to register it
        var rules = typeof(RuleOrchestrator).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && ruleType.IsAssignableFrom(t))
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .Where(r => IsRuleEnabled(r.Id, config))
            .ToList();

        // Wire config into rules that need it
        foreach (var rule in rules.OfType<IConfigurableRule>())
            rule.Configure(config);

        return new RuleOrchestrator(rules, config, ruleTimeout);
    }

    /// <summary>
    /// Runs all registered rules against the diff, applies ignore lists and severity overrides,
    /// and returns aggregated findings with per-rule metrics.
    /// </summary>
    /// <param name="diff">The parsed diff to analyze.</param>
    /// <param name="staticAnalysis">Optional Roslyn diagnostics to make available to rules.</param>
    /// <param name="ignoreList">Optional suppression list; matching findings are removed from results.</param>
    /// <param name="ct">Token used to cancel the entire evaluation run.</param>
    /// <returns>An <see cref="EvaluationResult"/> containing all findings and execution metadata.</returns>
    public async Task<EvaluationResult> RunAsync(
        DiffContext diff,
        AnalyzerResult? staticAnalysis = null,
        IgnoreList? ignoreList = null,
        CancellationToken ct = default)
    {
        // Classify all changed files and split into eligible/skipped before any rule runs
        var allRecords = diff.Files
            .Select(f => _fileAnalyzer.Analyze(f))
            .ToList();

        var eligibleRecords = allRecords
            .Where(r => r.IsEligible &&
                        r.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var skippedRecords  = allRecords
            .Where(r => !r.IsEligible ||
                        !r.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var fileStatistics  = FileEligibilityStatistics.From(allRecords);

        var eligibleFilePaths = eligibleRecords.Select(r => r.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredDiff = new DiffContext
        {
            RawDiff       = diff.RawDiff,
            CommitSha     = diff.CommitSha,
            CommitMessage = diff.CommitMessage,
            Files         = diff.Files.Where(f => eligibleFilePaths.Contains(f.NewPath)).ToList(),
        };

        var context = new AnalysisContext
        {
            EligibleFiles  = eligibleRecords,
            SkippedFiles   = skippedRecords,
            FileStatistics = fileStatistics,
            Diff           = filteredDiff,
            StaticAnalysis = staticAnalysis,
        };

        var allFindings = new List<Finding>();
        var metrics     = new List<RuleExecutionMetric>();

        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            using var ruleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ruleCts.CancelAfter(_ruleTimeout);
            var sw = Stopwatch.StartNew();
            var outcome = RuleOutcome.Passed;
            int findingsBefore = allFindings.Count;
            try
            {
                var findings = await rule.EvaluateAsync(context, ruleCts.Token);
                allFindings.AddRange(findings);
                if (findings.Count > 0) outcome = RuleOutcome.Triggered;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                outcome = RuleOutcome.TimedOut;
                Console.Error.WriteLine($"[GauntletCI] Rule {rule.Id} timed out after {_ruleTimeout.TotalSeconds:0}s — analysis truncated.");
                allFindings.Add(new Finding
                {
                    RuleId          = rule.Id,
                    RuleName        = rule.Name,
                    Summary         = $"Rule {rule.Id} timed out after {_ruleTimeout.TotalSeconds:0}s — results may be incomplete.",
                    Evidence        = $"Analysis exceeded the {_ruleTimeout.TotalSeconds:0}-second per-rule time limit.",
                    WhyItMatters    = "A timeout may indicate pathologically complex diff input (Roslyn Bomb) or a hung analyzer.",
                    SuggestedAction = "Investigate the diff for unusual patterns or report this as a GauntletCI issue.",
                    Confidence      = Confidence.Medium,
                });
            }
            catch (Exception ex)
            {
                outcome = RuleOutcome.Errored;
                Console.Error.WriteLine($"[GauntletCI] Rule {rule.Id} threw an exception: {ex.Message}");
                allFindings.Add(new Finding
                {
                    RuleId          = rule.Id,
                    RuleName        = rule.Name,
                    Summary         = $"Rule {rule.Id} encountered an internal error and could not complete analysis.",
                    Evidence        = ex.GetType().Name + ": " + ex.Message,
                    WhyItMatters    = "An errored rule may have missed real issues in this diff.",
                    SuggestedAction = "Report this error at https://github.com/EricCogen/GauntletCI/issues.",
                    Confidence      = Confidence.Low,
                });
            }
            finally
            {
                sw.Stop();
            }
            metrics.Add(new RuleExecutionMetric(rule.Id, sw.ElapsedMilliseconds, outcome, allFindings.Count - findingsBefore));
        }

        ApplySeverityOverrides(allFindings, _config);
        ApplyIgnoreList(allFindings, ignoreList);
        PostProcess(filteredDiff, allFindings);

        return new EvaluationResult
        {
            CommitSha      = diff.CommitSha,
            Findings       = allFindings,
            RulesEvaluated = _rules.Count,
            RuleMetrics    = metrics,
            FileStatistics = fileStatistics,
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
        findings.RemoveAll(f => ignoreList.IsSuppressed(f.RuleId, f.FilePath));
    }

    /// <summary>
    /// Runs synthesis checks after all rules have completed.
    /// Adds GCI0018 aggregate finding and runs any <see cref="IPostProcessor"/> rules.
    /// </summary>
    private void PostProcess(DiffContext diff, List<Finding> allFindings)
    {
        try
        {
            int distinctRulesFired = allFindings.Select(f => f.RuleId).Distinct().Count();
            if (distinctRulesFired > 3)
            {
                allFindings.Add(new Finding
                {
                    RuleId          = "GCI_SYN_AGG",
                    RuleName        = "Aggregate Risk Signal",
                    Summary         = $"{distinctRulesFired} rules flagged issues — this diff has multiple risk dimensions.",
                    Evidence        = $"Rules fired: {string.Join(", ", allFindings.Select(f => f.RuleId).Distinct().OrderBy(id => id))}",
                    WhyItMatters    = "Multiple concurrent risks compound each other and increase the chance of production incidents.",
                    SuggestedAction = "Address High-confidence findings first, then revisit Medium/Low before merging.",
                    Confidence      = Confidence.Medium,
                });
            }

            foreach (var processor in _rules.OfType<IPostProcessor>())
            {
                var finding = processor.PostProcess(diff);
                if (finding != null) allFindings.Add(finding);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GauntletCI] PostProcess threw an exception: {ex.Message}");
        }
    }
}

/// <summary>Aggregated output of a single <see cref="RuleOrchestrator.RunAsync"/> call.</summary>
public class EvaluationResult
{
    /// <summary>The Git commit SHA that was analyzed, or a sentinel such as "staged".</summary>
    public string CommitSha { get; init; } = string.Empty;
    /// <summary>All findings raised by any rule during this evaluation run.</summary>
    public List<Finding> Findings { get; init; } = [];
    /// <summary>The total number of rules that were executed.</summary>
    public int RulesEvaluated { get; init; }
    /// <summary>Per-rule timing and outcome metrics for diagnostics and reporting.</summary>
    public IReadOnlyList<RuleExecutionMetric> RuleMetrics { get; init; } = [];
    /// <summary>Summary of how each changed file was classified for eligibility.</summary>
    public FileEligibilityStatistics FileStatistics { get; init; } = new();
    /// <summary>True when at least one finding was produced by this evaluation run.</summary>
    public bool HasFindings => Findings.Count > 0;
}

/// <summary>The outcome of a single rule's execution within a run.</summary>
public enum RuleOutcome { Passed, Triggered, TimedOut, Errored }

/// <summary>Per-rule execution timing and outcome, attached to every <see cref="EvaluationResult"/>.</summary>
public record RuleExecutionMetric(string RuleId, long DurationMs, RuleOutcome Outcome, int FindingCount);
