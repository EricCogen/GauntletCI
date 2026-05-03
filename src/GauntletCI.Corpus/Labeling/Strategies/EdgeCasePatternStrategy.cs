// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for miscellaneous and edge-case patterns:
/// GCI0022 - Idempotency and retry safety
/// GCI0035 - Architecture layer violations
/// GCI0041 - Test coverage gaps
/// GCI0044 - Performance issues (LINQ in loops, allocations)
/// </summary>
public sealed class EdgeCasePatternStrategy : IInferenceStrategy
{
    /// <summary>
    /// Applies GCI0022, GCI0035, GCI0041, GCI0044 heuristics.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        // GCI0022 -- Idempotency and retry safety
        bool hasRemovedIdempotencyKey = context.ProductionRemovedLines.Any(l =>
            l.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("IdempotencyKey", StringComparison.Ordinal) ||
            l.Contains("DuplicateRequestHandler", StringComparison.Ordinal) ||
            l.Contains("INSERT OR IGNORE", StringComparison.Ordinal) ||
            l.Contains("upsert", StringComparison.OrdinalIgnoreCase));

        if (hasRemovedIdempotencyKey)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0022",
                ShouldTrigger = true,
                ExpectedConfidence = 0.65,
                Reason = "Diff removes idempotency key or duplicate-request handling logic",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0035 -- Architecture layer violations
        bool hasLayerViolation = context.AddedLines.Any(l =>
        {
            if (l.TrimStart().StartsWith("//"))
                return false;
            
            // Check for cross-layer dependencies
            return (l.Contains("UI", StringComparison.OrdinalIgnoreCase) && l.Contains("Repository", StringComparison.Ordinal)) ||
                   (l.Contains("View", StringComparison.Ordinal) && l.Contains("Service", StringComparison.Ordinal)) ||
                   (l.Contains("Database", StringComparison.OrdinalIgnoreCase) && l.Contains("Controller", StringComparison.Ordinal));
        });

        if (hasLayerViolation)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0035",
                ShouldTrigger = true,
                ExpectedConfidence = 0.55,
                Reason = "Diff may contain cross-layer architecture violation",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0041 -- Test coverage gaps
        bool hasRemovedTestMethod = context.RemovedLines.Any(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("[Fact]", StringComparison.Ordinal) ||
                   t.StartsWith("[Theory]", StringComparison.Ordinal) ||
                   t.StartsWith("public void Test", StringComparison.Ordinal) ||
                   t.StartsWith("public async Task Test", StringComparison.Ordinal);
        });

        bool hasRemovedAssertion = context.ProductionRemovedLines.Any(l =>
            l.Contains("Assert.", StringComparison.Ordinal) ||
            l.Contains("Should.", StringComparison.Ordinal) ||
            l.Contains(".ShouldBe", StringComparison.Ordinal));

        if (hasRemovedTestMethod || hasRemovedAssertion)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0041",
                ShouldTrigger = true,
                ExpectedConfidence = 0.60,
                Reason = "Diff removes test methods or assertions, reducing test coverage",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0044 -- Performance issues (LINQ in loops, allocations)
        bool hasLinqInLoop = context.AddedLines.Any(l =>
            (l.Contains(".Where(", StringComparison.Ordinal) ||
             l.Contains(".Select(", StringComparison.Ordinal) ||
             l.Contains(".FirstOrDefault(", StringComparison.Ordinal)) &&
            context.AddedLines.Any(line => line.Contains("for ", StringComparison.Ordinal) ||
                                           line.Contains("foreach", StringComparison.Ordinal)));

        bool hasAllocationInHotPath = context.AddedLines.Any(l =>
            (l.Contains("new List<", StringComparison.Ordinal) ||
             l.Contains("new Dictionary<", StringComparison.Ordinal) ||
             l.Contains("new string", StringComparison.Ordinal)) &&
            context.PathLines.Any(p => p.Contains("Loop", StringComparison.OrdinalIgnoreCase) ||
                                       p.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
                                       p.Contains("Handler", StringComparison.OrdinalIgnoreCase)));

        if (hasLinqInLoop || hasAllocationInHotPath)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0044",
                ShouldTrigger = true,
                ExpectedConfidence = 0.60,
                Reason = "Diff may introduce performance issue (LINQ in loop or allocation in hot path)",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }
}
