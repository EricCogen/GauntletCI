// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for nullability-related heuristics:
/// GCI0006 - Null dereference risks
/// GCI0043 - Nullable reference type and null-forgiving operator usage
/// </summary>
public sealed class NullabilityPatternStrategy : IInferenceStrategy
{
    /// <summary>
    /// Applies GCI0006 and GCI0043 heuristics.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        // GCI0006 -- Null dereference risks
        bool hasUnsafeNullAssignment = context.ProductionAddedLines.Any(l =>
        {
            if (l.TrimStart().StartsWith("//"))
                return false;
            
            // Pattern: variable = null without explicit nullable type annotation
            return l.Contains(" = null", StringComparison.Ordinal) &&
                   !l.Contains("?", StringComparison.Ordinal); // No ? for nullable
        });

        bool hasRemovedNullGuard = context.ProductionRemovedLines.Any(l =>
            l.Contains("??", StringComparison.Ordinal) || // Null-coalescing
            l.Contains("?.") || // Null-conditional
            l.Contains("!= null", StringComparison.Ordinal) ||
            l.Contains("== null", StringComparison.Ordinal));

        if (hasUnsafeNullAssignment || hasRemovedNullGuard)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0006",
                ShouldTrigger = true,
                ExpectedConfidence = 0.60,
                Reason = "Diff contains potential null dereference or unsafe null handling",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0043 -- Nullable reference types and null-forgiving operators
        bool hasNullForgivingOperator = context.AddedLines.Any(l =>
        {
            if (l.TrimStart().StartsWith("//"))
                return false;
            // Null-forgiving operator is just ! by itself after an expression
            // Pattern: it's typically after ) or an identifier
            return l.Contains(")!", StringComparison.Ordinal) ||  // method call!
                   l.Contains("!;", StringComparison.Ordinal) ||  // var x = expr!;
                   l.Contains("!,", StringComparison.Ordinal) ||  // in argument list
                   l.Contains("!.", StringComparison.Ordinal);    // chained property!.Property
        });

        bool hasRemovedNullForgiving = context.ProductionRemovedLines.Any(l =>
        {
            if (l.TrimStart().StartsWith("//"))
                return false;
            return l.Contains(")!", StringComparison.Ordinal) ||
                   l.Contains("!;", StringComparison.Ordinal) ||
                   l.Contains("!,", StringComparison.Ordinal) ||
                   l.Contains("!.", StringComparison.Ordinal);
        });

        bool hasPragmaNullableDisable = context.AddedLines.Any(l =>
            l.Contains("#pragma warning disable nullable", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("CS8600", StringComparison.Ordinal) ||
            l.Contains("CS8602", StringComparison.Ordinal));

        if (hasNullForgivingOperator || hasPragmaNullableDisable || hasRemovedNullForgiving)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0043",
                ShouldTrigger = true,
                ExpectedConfidence = 0.60,
                Reason = "Diff contains null-forgiving operator or nullable warning suppression",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }
}
