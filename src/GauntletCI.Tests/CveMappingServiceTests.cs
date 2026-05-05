// SPDX-License-Identifier: Elastic-2.0
using System;
using System.Threading.Tasks;
using GauntletCI.Corpus.CveMapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GauntletCI.Corpus.Tests;

/// <summary>
/// Unit tests for CVE-to-finding mapping service.
/// </summary>
public sealed class CveMappingServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private CveMappingService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _service = new CveMappingService(_connection);
        
        // Create minimal schema for testing
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS cve_to_findings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                cve_id TEXT NOT NULL,
                cvss_score REAL,
                cwe_id TEXT,
                affected_package TEXT,
                vulnerable_version TEXT,
                fixture_id TEXT NOT NULL,
                repository TEXT NOT NULL,
                finding_rule_id TEXT,
                finding_id TEXT,
                finding_confidence REAL DEFAULT 0.5,
                detected_by_dependabot INTEGER DEFAULT 0,
                detected_by_gci INTEGER DEFAULT 0,
                gci_detected_exploit INTEGER DEFAULT 0,
                detected_by_codeql INTEGER DEFAULT 0,
                detected_by_semgrep INTEGER DEFAULT 0,
                UNIQUE(cve_id, fixture_id, finding_rule_id)
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetRuleEffectivenessAsync_WithNoData_ReturnsZeroMetrics()
    {
        var metric = await _service.GetRuleEffectivenessAsync("GCI0048");
        
        Assert.Equal("GCI0048", metric.RuleId);
        Assert.Equal(0, metric.TotalCvesFound);
        Assert.Equal(0, metric.DetectedCount);
    }

    [Fact]
    public async Task GetRuleEffectivenessAsync_WithData_ReturnsMetrics()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO cve_to_findings 
                (cve_id, fixture_id, repository, finding_rule_id, finding_confidence, gci_detected_exploit)
                VALUES 
                ('CVE-2024-1234', 'fix1', 'repo1', 'GCI0048', 0.85, 1),
                ('CVE-2024-1235', 'fix2', 'repo1', 'GCI0048', 0.90, 1),
                ('CVE-2024-1236', 'fix3', 'repo1', 'GCI0048', 0.75, 0);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Act
        var metric = await _service.GetRuleEffectivenessAsync("GCI0048");

        // Assert
        Assert.Equal("GCI0048", metric.RuleId);
        Assert.Equal(3, metric.TotalCvesFound);
        Assert.Equal(2, metric.DetectedCount);
        Assert.Equal(1, metric.MissedCount);
        Assert.InRange(metric.CoveragePct, 65.0, 70.0); // ~66.67%
        Assert.InRange(metric.AvgConfidence, 0.833, 0.834); // avg(0.85, 0.9, 0.75)
    }

    [Fact]
    public async Task GetAllRuleEffectivenessAsync_WithMultipleRules_ReturnsAllRanked()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO cve_to_findings 
                (cve_id, fixture_id, repository, finding_rule_id, finding_confidence, gci_detected_exploit)
                VALUES 
                ('CVE-1', 'fix1', 'repo1', 'GCI0048', 0.85, 1),
                ('CVE-2', 'fix2', 'repo1', 'GCI0048', 0.90, 1),
                ('CVE-3', 'fix3', 'repo1', 'GCI0012', 0.95, 1),
                ('CVE-4', 'fix4', 'repo1', 'GCI0012', 0.92, 1),
                ('CVE-5', 'fix5', 'repo1', 'GCI0012', 0.88, 0);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Act
        var metrics = await _service.GetAllRuleEffectivenessAsync();

        // Assert
        Assert.Equal(2, metrics.Count);
        // GCI0012 has 66.67% coverage (2/3), GCI0048 has 100% coverage (2/2)
        // Should be ordered by coverage (highest first)
        Assert.Equal("GCI0048", metrics[0].RuleId);
        Assert.Equal(2, metrics[0].TotalCvesFound);
        Assert.Equal(2, metrics[0].DetectedCount);
        Assert.Equal("GCI0012", metrics[1].RuleId);
        Assert.Equal(3, metrics[1].TotalCvesFound);
        Assert.Equal(2, metrics[1].DetectedCount);
    }

    [Fact]
    public async Task GetCriticalCveFixturesAsync_WithDependabotAndGci_ReturnsCritical()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO cve_to_findings 
                (cve_id, cvss_score, fixture_id, repository, finding_rule_id, 
                 finding_confidence, detected_by_dependabot, gci_detected_exploit)
                VALUES 
                ('CVE-2024-1000', 9.8, 'fix-critical', 'repo1', 'GCI0048', 0.95, 1, 1),
                ('CVE-2024-1001', 8.5, 'fix-critical', 'repo1', 'GCI0048', 0.92, 1, 1);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Act
        var findings = await _service.GetCriticalCveFixturesAsync();

        // Assert
        Assert.Single(findings);
        Assert.Equal("fix-critical", findings[0].FixtureId);
        Assert.Equal(2, findings[0].CveCount);
        Assert.InRange(findings[0].MaxSeverity, 9.7, 9.9);
        Assert.Contains("GCI0048", findings[0].MatchingRules);
    }

    [Fact]
    public async Task GetLatentCveFindings_WithGciOnlyDetection_ReturnsLatent()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO cve_to_findings 
                (cve_id, fixture_id, repository, finding_rule_id, finding_confidence, 
                 detected_by_dependabot, gci_detected_exploit)
                VALUES 
                ('CVE-2024-2000', 'fix1', 'repo1', 'GCI0048', 0.88, 0, 1),
                ('CVE-2024-2000', 'fix2', 'repo1', 'GCI0048', 0.85, 0, 1),
                ('CVE-2024-2001', 'fix3', 'repo1', 'GCI0012', 0.92, 0, 1);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Act
        var findings = await _service.GetLatentCveFindings();

        // Assert
        Assert.Equal(2, findings.Count);
        var finding1 = findings.Find(f => f.CveId == "CVE-2024-2000");
        Assert.NotNull(finding1);
        Assert.Equal(2, finding1.OccurrenceCount);
    }

    [Fact]
    public async Task GetToolComparisonAsync_WithMixedDetections_ReturnsComparison()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO cve_to_findings 
                (cve_id, fixture_id, repository, finding_rule_id, 
                 detected_by_gci, detected_by_dependabot, detected_by_codeql, detected_by_semgrep)
                VALUES 
                ('CVE-1', 'fix1', 'repo1', 'GCI0048', 1, 1, 0, 0),
                ('CVE-2', 'fix2', 'repo1', 'GCI0048', 1, 0, 1, 0),
                ('CVE-3', 'fix3', 'repo1', 'GCI0048', 1, 1, 0, 1),
                ('CVE-4', 'fix4', 'repo1', 'GCI0048', 0, 1, 1, 1);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Act
        var comparison = await _service.GetToolComparisonAsync();

        // Assert
        Assert.Equal(4, comparison.TotalUniqueCves);
        Assert.Equal(3, comparison.GciCaught);
        Assert.Equal(3, comparison.DependabotCaught);
        Assert.Equal(2, comparison.CodeqlCaught);
        Assert.Equal(2, comparison.SemgrepCaught);
        Assert.InRange(comparison.GciCoveragePct, 74.0, 76.0); // 75%
    }
}
