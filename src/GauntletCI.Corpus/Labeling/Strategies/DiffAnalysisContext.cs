// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Pre-parsed diff components passed to inference strategies.
/// Avoids redundant parsing and allows strategies to work with common normalized data.
/// </summary>
public sealed class DiffAnalysisContext
{
    /// <summary>All lines added in the diff.</summary>
    public IReadOnlyList<string> AddedLines { get; init; } = [];

    /// <summary>All lines removed in the diff.</summary>
    public IReadOnlyList<string> RemovedLines { get; init; } = [];

    /// <summary>File paths mentioned in diff hunks (e.g., file headers).</summary>
    public IReadOnlyList<string> PathLines { get; init; } = [];

    /// <summary>Added lines from .cs files in src/ or production paths (not tests).</summary>
    public IReadOnlyList<string> ProductionAddedLines { get; init; } = [];

    /// <summary>Removed lines from .cs files in src/ or production paths (not tests).</summary>
    public IReadOnlyList<string> ProductionRemovedLines { get; init; } = [];

    /// <summary>The full raw diff text for detailed analysis when needed.</summary>
    public string RawDiff { get; init; } = string.Empty;
}
