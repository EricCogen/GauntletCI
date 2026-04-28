// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Cli.Audit;

/// <summary>
/// A single scan record written to the local audit log.
/// Contains full finding detail: not anonymised like telemetry.
/// </summary>
public record AuditLogEntry
{
    public string ScanId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string RepoPath { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public string DiffSource { get; init; } = string.Empty;
    public int FilesChanged { get; init; }
    public int FilesEligible { get; init; }
    public int RulesEvaluated { get; init; }
    public int FindingCount { get; init; }
    public IReadOnlyList<AuditFinding> Findings { get; init; } = [];
}

public record AuditFinding
{
    public string RuleId { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string? FilePath { get; init; }
    public int? Line { get; init; }
}
