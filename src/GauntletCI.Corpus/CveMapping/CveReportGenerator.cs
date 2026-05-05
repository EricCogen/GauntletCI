// SPDX-License-Identifier: Elastic-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GauntletCI.Corpus.CveMapping;

/// <summary>
/// Generates CVE effectiveness reports and dashboards for display.
/// </summary>
public sealed class CveReportGenerator
{
    private readonly CveMappingService _service;

    public CveReportGenerator(CveMappingService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Generate a comprehensive CVE effectiveness report.
    /// </summary>
    public async Task<string> GenerateComprehensiveReportAsync(
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           CVE EFFECTIVENESS REPORT - GCI vs Tools              ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        
        // Tool comparison
        sb.AppendLine(await GenerateToolComparisonAsync(cancellationToken).ConfigureAwait(false));
        sb.AppendLine();
        
        // Rule effectiveness
        sb.AppendLine(await GenerateRuleEffectivenessAsync(cancellationToken).ConfigureAwait(false));
        sb.AppendLine();
        
        // Critical findings
        sb.AppendLine(await GenerateCriticalFindingsAsync(cancellationToken).ConfigureAwait(false));
        sb.AppendLine();
        
        // Latent findings
        sb.AppendLine(await GenerateLatentFindingsAsync(cancellationToken).ConfigureAwait(false));
        
        return sb.ToString();
    }

    /// <summary>
    /// Generate tool comparison section.
    /// </summary>
    public async Task<string> GenerateToolComparisonAsync(
        CancellationToken cancellationToken = default)
    {
        var comparison = await _service.GetToolComparisonAsync(cancellationToken).ConfigureAwait(false);
        
        var sb = new StringBuilder();
        sb.AppendLine("📊 TOOL COMPARISON");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        if (comparison.TotalUniqueCves == 0)
        {
            sb.AppendLine("No CVE data available yet.");
            return sb.ToString();
        }
        
        sb.AppendLine($"Total Unique CVEs: {comparison.TotalUniqueCves}");
        sb.AppendLine();
        
        var tools = new[]
        {
            ("GCI",          comparison.GciCaught,         comparison.GciCoveragePct),
            ("Dependabot",   comparison.DependabotCaught,  comparison.DependabotCoveragePct),
            ("CodeQL",       comparison.CodeqlCaught,      comparison.CodeqlCoveragePct),
            ("Semgrep",      comparison.SemgrepCaught,     comparison.SemgrepCoveragePct),
        };
        
        foreach (var (name, caught, pct) in tools.OrderByDescending(t => t.Item3))
        {
            var bar = GenerateBar(pct / 100.0, 20);
            sb.AppendLine($"{name,-12} {caught:D2}/{comparison.TotalUniqueCves:D2}  {bar}  {pct:F1}%");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generate rule effectiveness section.
    /// </summary>
    public async Task<string> GenerateRuleEffectivenessAsync(
        CancellationToken cancellationToken = default)
    {
        var metrics = await _service.GetAllRuleEffectivenessAsync(cancellationToken).ConfigureAwait(false);
        
        var sb = new StringBuilder();
        sb.AppendLine("🎯 RULE EFFECTIVENESS (on CVE Detection)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        if (!metrics.Any())
        {
            sb.AppendLine("No rule metrics available yet.");
            return sb.ToString();
        }
        
        sb.AppendLine("Rule         Caught  Total  Coverage  Confidence");
        sb.AppendLine("──────────────────────────────────────────────");
        
        foreach (var metric in metrics)
        {
            var bar = GenerateBar(metric.CoveragePct / 100.0, 15);
            sb.AppendLine($"{metric.RuleId,-8}  {metric.DetectedCount:D2}/{metric.TotalCvesFound:D2}     {bar}  {metric.CoveragePct:F1}%    {metric.AvgConfidence:F3}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generate critical findings section (Dependabot + GCI both detected).
    /// </summary>
    public async Task<string> GenerateCriticalFindingsAsync(
        CancellationToken cancellationToken = default)
    {
        var findings = await _service.GetCriticalCveFixturesAsync(cancellationToken).ConfigureAwait(false);
        
        var sb = new StringBuilder();
        sb.AppendLine("⚠️  CRITICAL: Dependabot + GCI Both Detected");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        if (!findings.Any())
        {
            sb.AppendLine("No critical findings (vulnerable package + exploitable code).");
            return sb.ToString();
        }
        
        sb.AppendLine($"Found {findings.Count} critical scenario(s):");
        sb.AppendLine();
        
        foreach (var finding in findings.Take(10))
        {
            var severity = GetSeverityLabel(finding.MaxSeverity);
            sb.AppendLine($"  Fixture: {finding.FixtureId}");
            sb.AppendLine($"  Repository: {finding.Repository}");
            sb.AppendLine($"  CVEs: {finding.CveIds}");
            sb.AppendLine($"  Max Severity: {severity} ({finding.MaxSeverity:F1})");
            sb.AppendLine($"  Matching Rules: {finding.MatchingRules}");
            sb.AppendLine();
        }
        
        if (findings.Count > 10)
        {
            sb.AppendLine($"  ... and {findings.Count - 10} more critical findings");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generate latent findings section (GCI caught it, Dependabot missed).
    /// </summary>
    public async Task<string> GenerateLatentFindingsAsync(
        CancellationToken cancellationToken = default)
    {
        var findings = await _service.GetLatentCveFindings(cancellationToken).ConfigureAwait(false);
        
        var sb = new StringBuilder();
        sb.AppendLine("🔍 LATENT: GCI Caught It, Dependabot Missed");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        
        if (!findings.Any())
        {
            sb.AppendLine("No latent findings (GCI-only detections).");
            return sb.ToString();
        }
        
        sb.AppendLine($"Found {findings.Count} latent vulnerability pattern(s):");
        sb.AppendLine();
        
        foreach (var finding in findings.Take(10))
        {
            sb.AppendLine($"  {finding.CveId} ({finding.RuleId})");
            sb.AppendLine($"    Occurrences: {finding.OccurrenceCount}");
            sb.AppendLine($"    Avg Confidence: {finding.AvgConfidence:F3}");
            sb.AppendLine();
        }
        
        if (findings.Count > 10)
        {
            sb.AppendLine($"  ... and {findings.Count - 10} more latent findings");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generate a concise dashboard suitable for dashboards/alerts.
    /// </summary>
    public async Task<DashboardSummary> GenerateDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var comparison = await _service.GetToolComparisonAsync(cancellationToken).ConfigureAwait(false);
        var ruleMetrics = await _service.GetAllRuleEffectivenessAsync(cancellationToken).ConfigureAwait(false);
        var critical = await _service.GetCriticalCveFixturesAsync(cancellationToken).ConfigureAwait(false);
        var latent = await _service.GetLatentCveFindings(cancellationToken).ConfigureAwait(false);
        
        return new DashboardSummary
        {
            TotalUniqueCves = comparison.TotalUniqueCves,
            GciCoveragePercent = comparison.GciCoveragePct,
            DependabotCoveragePercent = comparison.DependabotCoveragePct,
            CodeqlCoveragePercent = comparison.CodeqlCoveragePct,
            SemgrepCoveragePercent = comparison.SemgrepCoveragePct,
            TopRule = ruleMetrics.FirstOrDefault()?.RuleId ?? "N/A",
            TopRuleEffectiveness = ruleMetrics.FirstOrDefault()?.CoveragePct ?? 0,
            CriticalFindings = critical.Count,
            LatentFindings = latent.Count,
            AllRuleMetrics = ruleMetrics
        };
    }

    private static string GenerateBar(double ratio, int width)
    {
        var filled = (int)(ratio * width);
        var empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    private static string GetSeverityLabel(double cvssScore) => cvssScore switch
    {
        >= 9.0 => "CRITICAL",
        >= 7.0 => "HIGH",
        >= 4.0 => "MEDIUM",
        >= 0.1 => "LOW",
        _ => "INFO"
    };
}

/// <summary>
/// Summary dashboard data suitable for visualization/alerts.
/// </summary>
public sealed class DashboardSummary
{
    public int TotalUniqueCves { get; set; }
    public double GciCoveragePercent { get; set; }
    public double DependabotCoveragePercent { get; set; }
    public double CodeqlCoveragePercent { get; set; }
    public double SemgrepCoveragePercent { get; set; }
    public string TopRule { get; set; } = "";
    public double TopRuleEffectiveness { get; set; }
    public int CriticalFindings { get; set; }
    public int LatentFindings { get; set; }
    public List<CveEffectivenessMetric> AllRuleMetrics { get; set; } = new();

    public override string ToString() =>
        $"CVE Dashboard: {TotalUniqueCves} CVEs, GCI {GciCoveragePercent:F1}%, " +
        $"{CriticalFindings} critical, {LatentFindings} latent";
}
