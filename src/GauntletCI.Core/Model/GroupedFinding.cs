// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.Model;

/// <summary>
/// Represents one or more <see cref="Finding"/>s produced by the same rule against the
/// same file, collapsed into a single logical entry for reporting.
/// Created by <see cref="FindingGrouper"/>.
/// </summary>
public sealed class GroupedFinding
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public required string Summary { get; init; }
    public required string WhyItMatters { get; init; }
    public required string SuggestedAction { get; init; }
    public Confidence Confidence { get; init; }
    public RuleSeverity Severity { get; init; }

    /// <summary>Path of the file containing the findings, or null when the rule emitted no path.</summary>
    public string? FilePath { get; init; }

    /// <summary>Primary line for inline annotations / PR comments. Lowest line number in the group.</summary>
    public int? PrimaryLine { get; init; }

    /// <summary>Distinct, sorted line numbers across all findings in the group.</summary>
    public required IReadOnlyList<int> Lines { get; init; }

    /// <summary>Distinct evidence strings, in first-seen order.</summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    /// <summary>Number of underlying findings collapsed into this group.</summary>
    public int Count { get; init; }

    public string? LlmExplanation { get; init; }
    public ExpertFact? ExpertContext { get; init; }
    public string? CodeSnippet { get; init; }
    public string? CoverageNote { get; init; }
    public TicketInfo? TicketContext { get; init; }
}
