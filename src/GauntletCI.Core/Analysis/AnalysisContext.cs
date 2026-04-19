// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Analysis;

/// <summary>
/// Shared context passed to every rule after file eligibility filtering.
/// Rules should consume <see cref="EligibleFiles"/> and <see cref="Diff"/> (pre-filtered to eligible files only).
/// </summary>
public sealed class AnalysisContext
{
    /// <summary>Classification records for files that passed the eligibility filter.</summary>
    public IReadOnlyList<ChangedFileAnalysisRecord> EligibleFiles { get; init; }
        = Array.Empty<ChangedFileAnalysisRecord>();

    /// <summary>Classification records for files that were skipped before rules ran.</summary>
    public IReadOnlyList<ChangedFileAnalysisRecord> SkippedFiles { get; init; }
        = Array.Empty<ChangedFileAnalysisRecord>();

    /// <summary>Aggregate counts from file classification.</summary>
    public FileEligibilityStatistics FileStatistics { get; init; } = new();

    /// <summary>
    /// DiffContext containing only the eligible files.
    /// Rules should iterate this instead of accessing raw PR file lists.
    /// </summary>
    public DiffContext Diff { get; init; } = new();

    /// <summary>Optional static analysis results for the diff.</summary>
    public AnalyzerResult? StaticAnalysis { get; init; }

    /// <summary>
    /// Primary target framework moniker detected from the repo's .csproj files
    /// (e.g. <c>net8.0</c>, <c>net9.0</c>). Null when detection was not possible.
    /// Rules use this to tailor suggested actions to the target platform.
    /// </summary>
    public string? TargetFramework { get; init; }
}
