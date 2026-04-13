// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

/// <summary>
/// Explicit outcome for a single (fixture, rule) evaluation.
/// Unknown replaces silent omission — no result is ever invisible.
/// </summary>
public enum EvaluationStatus
{
    TruePositive,
    FalsePositive,
    FalseNegative,
    TrueNegative,
    /// <summary>Rule fired but no label exists for this fixture/rule pair.</summary>
    Unknown,
}

/// <summary>Confidence tier of the label that drove this evaluation.</summary>
public enum LabelConfidence
{
    /// <summary>Human-reviewed — suitable for headline metrics.</summary>
    Trusted,
    /// <summary>Heuristic or weak supervision signal — directional only.</summary>
    Heuristic,
    /// <summary>No label available.</summary>
    Unknown,
}
