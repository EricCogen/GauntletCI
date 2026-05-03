// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for async execution model violations:
/// GCI0016 - Blocking calls on async paths, async void, lock(this), Thread.Sleep in production.
/// </summary>
public sealed class AsyncPatternStrategy : IInferenceStrategy
{
    public IReadOnlySet<string> RuleIds => new HashSet<string> { "GCI0016" };

    /// <summary>
    /// Applies GCI0016 heuristics: blocking calls, async void, lock(this), Thread.Sleep.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        bool isTestPath = context.PathLines.Any(l =>
            l.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Spec", StringComparison.OrdinalIgnoreCase));

        bool hasBlockingCall = context.AddedLines.Any(l =>
            l.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal) ||
            l.Contains(".Wait()", StringComparison.Ordinal) ||
            (l.Contains(".Result", StringComparison.Ordinal) &&
                (l.Contains("Task", StringComparison.Ordinal) ||
                 l.Contains("Async", StringComparison.Ordinal))) ||
            (l.Contains(".Wait(", StringComparison.Ordinal) &&
                (l.Contains("Task", StringComparison.Ordinal) ||
                 l.Contains("CancellationToken", StringComparison.Ordinal))));

        bool hasAsyncVoid = context.AddedLines.Any(l =>
            l.Contains("async void ", StringComparison.Ordinal) &&
            !l.Contains("EventArgs", StringComparison.Ordinal) &&
            !l.Contains("object sender", StringComparison.Ordinal));

        bool hasLockThis = context.AddedLines.Any(l =>
            l.Contains("lock(this)", StringComparison.Ordinal) ||
            l.Contains("lock (this)", StringComparison.Ordinal));

        bool hasThreadSleep = !isTestPath && context.AddedLines.Any(l =>
            l.Contains("Thread.Sleep(", StringComparison.Ordinal) &&
            !l.TrimStart().StartsWith("//"));

        if (hasBlockingCall || hasAsyncVoid || hasLockThis || hasThreadSleep)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0016",
                ShouldTrigger = true,
                ExpectedConfidence = 0.65,
                Reason = "Diff contains async execution model violation (blocking call, async void, lock(this), or Thread.Sleep)",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }
}
