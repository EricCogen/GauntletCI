// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// A single anonymous telemetry event.
/// No PII, no file paths, no code content.
/// </summary>
public record TelemetryEvent
{
    public string EventId      { get; init; } = Guid.NewGuid().ToString();
    /// <summary>"analysis" | "finding" | "feedback"</summary>
    public string EventType    { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string InstallId    { get; init; } = string.Empty;
    /// <summary>8-char SHA-256 prefix of the git remote URL.</summary>
    public string RepoHash     { get; init; } = string.Empty;

    // analysis event fields
    public int?   FindingCount   { get; init; }
    public int?   FilesChanged   { get; init; }
    public int?   RulesEvaluated { get; init; }
    public int?   LinesAdded     { get; init; }
    public int?   LinesRemoved   { get; init; }

    // finding event fields
    public string? RuleId     { get; init; }
    public string? Confidence { get; init; }
    /// <summary>File extension only: never the full path.</summary>
    public string? FileExt    { get; init; }

    // rule_metric event fields
    public long?   DurationMs  { get; init; }
    public string? Outcome     { get; init; }

    // feedback event fields
    /// <summary>"up" | "down"</summary>
    public string? Vote { get; init; }

    public bool Sent { get; init; }
}
