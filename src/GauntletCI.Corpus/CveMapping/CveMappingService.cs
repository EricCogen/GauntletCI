// SPDX-License-Identifier: Elastic-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.CveMapping;

/// <summary>
/// Service for populating and querying CVE-to-finding mappings.
/// Bridges CVE advisories with GCI rule detections to measure effectiveness.
/// </summary>
public sealed class CveMappingService
{
    private readonly SqliteConnection _connection;

    public CveMappingService(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Map NuGet CVE advisories to GCI findings for a fixture.
    /// Call after GCI labeling and enrichments are complete.
    /// </summary>
    public async Task MapCveToFindingsAsync(
        string fixtureId,
        string repository,
        CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO cve_to_findings (
                cve_id, cvss_score, cwe_id, affected_package, vulnerable_version,
                fixture_id, repository, finding_rule_id, finding_id, finding_confidence,
                detected_by_dependabot, detected_by_gci, gci_detected_exploit
            )
            SELECT
                DISTINCT
                json_extract(ne.advisories_json, '$.cve_id') as cve_id,
                CAST(json_extract(ne.advisories_json, '$.cvss_score') AS REAL) as cvss_score,
                json_extract(ne.advisories_json, '$.cwe_id') as cwe_id,
                json_extract(ne.advisories_json, '$.package_name') as affected_package,
                json_extract(ne.advisories_json, '$.vulnerable_version') as vulnerable_version,
                af.fixture_id,
                $repo as repository,
                af.rule_id as finding_rule_id,
                af.id as finding_id,
                af.actual_confidence as finding_confidence,
                CASE WHEN dm.id IS NOT NULL THEN 1 ELSE 0 END as detected_by_dependabot,
                CASE WHEN af.did_trigger = 1 THEN 1 ELSE 0 END as detected_by_gci,
                CASE WHEN af.did_trigger = 1 THEN 1 ELSE 0 END as gci_detected_exploit
            FROM nuget_advisory_enrichments ne
            JOIN actual_findings af ON af.fixture_id = ne.fixture_id
            LEFT JOIN dependabot_matches dm ON dm.fixture_id = ne.fixture_id
            WHERE ne.fixture_id = $fixture_id
              AND af.did_trigger = 1
              AND (af.rule_id IN ('GCI0012', 'GCI0048', 'GCI0050', 'GCI0029', 'GCI0015'))
            """;
        
        cmd.Parameters.AddWithValue("$fixture_id", fixtureId);
        cmd.Parameters.AddWithValue("$repo", repository);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get effectiveness metrics for a specific GCI rule on CVE detection.
    /// Example: "GCI0048 catches 91% of SQL injection CVEs (21/23)"
    /// </summary>
    public async Task<CveEffectivenessMetric> GetRuleEffectivenessAsync(
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(COUNT(DISTINCT cve_id), 0) as total_cves,
                COALESCE(SUM(CASE WHEN gci_detected_exploit = 1 THEN 1 ELSE 0 END), 0) as detected_count,
                COALESCE(SUM(CASE WHEN gci_detected_exploit = 0 THEN 1 ELSE 0 END), 0) as missed_count,
                COALESCE(ROUND(100.0 * SUM(CASE WHEN gci_detected_exploit = 1 THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0), 2), 0) as coverage_pct,
                COALESCE(ROUND(AVG(finding_confidence), 3), 0) as avg_confidence
            FROM cve_to_findings
            WHERE finding_rule_id = $rule_id
            """;
        
        cmd.Parameters.AddWithValue("$rule_id", ruleId);
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new CveEffectivenessMetric
            {
                RuleId = ruleId,
                TotalCvesFound = reader.GetInt32(0),
                DetectedCount = reader.GetInt32(1),
                MissedCount = reader.GetInt32(2),
                CoveragePct = reader.GetDouble(3),
                AvgConfidence = reader.GetDouble(4)
            };
        }

        return new CveEffectivenessMetric { RuleId = ruleId };
    }

    /// <summary>
    /// Get all effectiveness metrics across all security rules.
    /// </summary>
    public async Task<List<CveEffectivenessMetric>> GetAllRuleEffectivenessAsync(
        CancellationToken cancellationToken = default)
    {
        var metrics = new List<CveEffectivenessMetric>();
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                finding_rule_id,
                COUNT(DISTINCT cve_id) as total_cves,
                SUM(CASE WHEN gci_detected_exploit = 1 THEN 1 ELSE 0 END) as detected_count,
                SUM(CASE WHEN gci_detected_exploit = 0 THEN 1 ELSE 0 END) as missed_count,
                ROUND(100.0 * SUM(CASE WHEN gci_detected_exploit = 1 THEN 1 ELSE 0 END) / COUNT(*), 2) as coverage_pct,
                ROUND(AVG(finding_confidence), 3) as avg_confidence
            FROM cve_to_findings
            WHERE finding_rule_id IS NOT NULL
            GROUP BY finding_rule_id
            ORDER BY coverage_pct DESC
            """;
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            metrics.Add(new CveEffectivenessMetric
            {
                RuleId = reader.GetString(0),
                TotalCvesFound = reader.GetInt32(1),
                DetectedCount = reader.GetInt32(2),
                MissedCount = reader.GetInt32(3),
                CoveragePct = reader.GetDouble(4),
                AvgConfidence = reader.GetDouble(5)
            });
        }

        return metrics;
    }

    /// <summary>
    /// Find PRs where both Dependabot AND GCI detected the same CVE.
    /// These are the most critical: vulnerable package + exploitable code.
    /// </summary>
    public async Task<List<CriticalCveFinding>> GetCriticalCveFixturesAsync(
        CancellationToken cancellationToken = default)
    {
        var findings = new List<CriticalCveFinding>();
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                fixture_id,
                repository,
                COUNT(DISTINCT cve_id) as cve_count,
                GROUP_CONCAT(cve_id, ', ') as cve_ids,
                MAX(cvss_score) as max_severity,
                GROUP_CONCAT(finding_rule_id, ', ') as matching_rules
            FROM cve_to_findings
            WHERE detected_by_dependabot = 1 AND gci_detected_exploit = 1
            GROUP BY fixture_id
            ORDER BY max_severity DESC, cve_count DESC
            """;
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            findings.Add(new CriticalCveFinding
            {
                FixtureId = reader.GetString(0),
                Repository = reader.GetString(1),
                CveCount = reader.GetInt32(2),
                CveIds = reader.GetString(3),
                MaxSeverity = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                MatchingRules = reader.GetString(5)
            });
        }

        return findings;
    }

    /// <summary>
    /// Find CVEs that GCI caught but Dependabot missed.
    /// Indicates GCI's ability to detect latent vulnerabilities.
    /// </summary>
    public async Task<List<LatentCveFinding>> GetLatentCveFindings(
        CancellationToken cancellationToken = default)
    {
        var findings = new List<LatentCveFinding>();
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                DISTINCT cve_id,
                finding_rule_id,
                repository,
                COUNT(DISTINCT fixture_id) as occurrence_count,
                ROUND(AVG(finding_confidence), 3) as avg_confidence
            FROM cve_to_findings
            WHERE gci_detected_exploit = 1 AND detected_by_dependabot = 0
            GROUP BY cve_id, finding_rule_id
            ORDER BY occurrence_count DESC
            """;
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            findings.Add(new LatentCveFinding
            {
                CveId = reader.GetString(0),
                RuleId = reader.GetString(1),
                Repository = reader.GetString(2),
                OccurrenceCount = reader.GetInt32(3),
                AvgConfidence = reader.GetDouble(4)
            });
        }

        return findings;
    }

    /// <summary>
    /// Get comparative effectiveness: GCI vs Dependabot vs CodeQL vs Semgrep.
    /// </summary>
    public async Task<ToolComparisonMetric> GetToolComparisonAsync(
        CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(DISTINCT cve_id) as total_unique_cves,
                COUNT(DISTINCT CASE WHEN detected_by_gci = 1 THEN cve_id END) as gci_caught,
                COUNT(DISTINCT CASE WHEN detected_by_dependabot = 1 THEN cve_id END) as dependabot_caught,
                COUNT(DISTINCT CASE WHEN detected_by_codeql = 1 THEN cve_id END) as codeql_caught,
                COUNT(DISTINCT CASE WHEN detected_by_semgrep = 1 THEN cve_id END) as semgrep_caught
            FROM cve_to_findings
            """;
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var total = reader.GetInt32(0);
            return new ToolComparisonMetric
            {
                TotalUniqueCves = total,
                GciCaught = reader.GetInt32(1),
                DependabotCaught = reader.GetInt32(2),
                CodeqlCaught = reader.GetInt32(3),
                SemgrepCaught = reader.GetInt32(4),
                GciCoveragePct = total > 0 ? (100.0 * reader.GetInt32(1) / total) : 0,
                DependabotCoveragePct = total > 0 ? (100.0 * reader.GetInt32(2) / total) : 0,
                CodeqlCoveragePct = total > 0 ? (100.0 * reader.GetInt32(3) / total) : 0,
                SemgrepCoveragePct = total > 0 ? (100.0 * reader.GetInt32(4) / total) : 0
            };
        }

        return new ToolComparisonMetric();
    }
}

/// <summary>Effectiveness metric for a single GCI rule on CVE detection.</summary>
public sealed class CveEffectivenessMetric
{
    public string RuleId { get; set; } = "";
    public int TotalCvesFound { get; set; }
    public int DetectedCount { get; set; }
    public int MissedCount { get; set; }
    public double CoveragePct { get; set; }
    public double AvgConfidence { get; set; }

    public override string ToString() =>
        $"{RuleId}: {DetectedCount}/{TotalCvesFound} ({CoveragePct:F1}%) avg_conf={AvgConfidence:F3}";
}

/// <summary>CVE detected by both vulnerable package and exploitable code.</summary>
public sealed class CriticalCveFinding
{
    public string FixtureId { get; set; } = "";
    public string Repository { get; set; } = "";
    public int CveCount { get; set; }
    public string CveIds { get; set; } = "";
    public double MaxSeverity { get; set; }
    public string MatchingRules { get; set; } = "";
}

/// <summary>CVE detected by GCI but not by Dependabot (latent vulnerability).</summary>
public sealed class LatentCveFinding
{
    public string CveId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Repository { get; set; } = "";
    public int OccurrenceCount { get; set; }
    public double AvgConfidence { get; set; }
}

/// <summary>Comparison of CVE detection across multiple tools.</summary>
public sealed class ToolComparisonMetric
{
    public int TotalUniqueCves { get; set; }
    public int GciCaught { get; set; }
    public int DependabotCaught { get; set; }
    public int CodeqlCaught { get; set; }
    public int SemgrepCaught { get; set; }
    public double GciCoveragePct { get; set; }
    public double DependabotCoveragePct { get; set; }
    public double CodeqlCoveragePct { get; set; }
    public double SemgrepCoveragePct { get; set; }
}
