// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0016 – Concurrency and State Risk
/// Detects async void, blocking async calls, static mutable state, and deadlock risks.
/// </summary>
public class GCI0016_ConcurrencyAndStateRisk : RuleBase
{
    public override string Id => "GCI0016";
    public override string Name => "Concurrency and State Risk";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            // Auto-generated files are never hand-authored; concurrency patterns in them are noise.
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            bool isTest = WellKnownPatterns.IsTestFile(file.NewPath);

            foreach (var line in file.AddedLines)
            {
                if (line.Content.TrimStart().StartsWith("//")) continue;
                CheckAsyncVoid(line, findings);
                CheckBlockingAsyncCall(line, findings);
                CheckLockThis(line, findings);
                // Thread.Sleep in test code is legitimate timing control, not a thread-pool concern.
                if (!isTest) CheckThreadSleepInAsync(line, findings);
                CheckStaticMutableField(line, findings);
            }
        }

        return Task.FromResult(findings);
    }

    private void CheckAsyncVoid(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;
        if (!content.Contains("async void ", StringComparison.Ordinal)) return;

        // Don't flag event handlers (common legitimate use)
        bool isEventHandler = content.Contains("EventHandler", StringComparison.Ordinal) ||
                               content.Contains("object sender", StringComparison.Ordinal) ||
                               content.Contains("EventArgs", StringComparison.Ordinal) ||
                               content.Contains("sender,", StringComparison.Ordinal) ||
                               content.Contains("sender)", StringComparison.Ordinal);
        if (isEventHandler) return;

        findings.Add(CreateFinding(
            summary: "async void method detected (fire-and-forget with no exception propagation).",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: "async void methods cannot be awaited and their exceptions crash the process silently.",
            suggestedAction: "Change return type to async Task. Only use async void for top-level event handlers.",
            confidence: Confidence.High));
    }

    private void CheckBlockingAsyncCall(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;
        bool hasResult = content.Contains(".Result", StringComparison.Ordinal);
        bool hasWait = content.Contains(".Wait()", StringComparison.Ordinal) ||
                       content.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal);

        if (!hasResult && !hasWait) return;

        findings.Add(CreateFinding(
            summary: "Blocking async call (.Result / .Wait()) can cause deadlocks.",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: ".Result and .Wait() block the calling thread, risking deadlock in ASP.NET or UI contexts with a synchronization context.",
            suggestedAction: "Use await instead. If you must block, use .ConfigureAwait(false) and be aware of the risk.",
            confidence: Confidence.High));
    }

    private void CheckLockThis(DiffLine line, List<Finding> findings)
    {
        if (!line.Content.Contains("lock(this)", StringComparison.Ordinal) &&
            !line.Content.Contains("lock (this)", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(
            summary: "lock(this) antipattern detected.",
            evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
            whyItMatters: "Locking on 'this' exposes the lock object publicly, allowing external code to cause deadlocks.",
            suggestedAction: "Use a private readonly object _lock = new object(); and lock on that.",
            confidence: Confidence.Medium));
    }

    private void CheckThreadSleepInAsync(DiffLine line, List<Finding> findings)
    {
        if (!line.Content.Contains("Thread.Sleep(", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(
            summary: "Thread.Sleep() in async context blocks a thread pool thread.",
            evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
            whyItMatters: "Thread.Sleep in async code wastes thread pool threads, degrading scalability.",
            suggestedAction: "Replace Thread.Sleep() with await Task.Delay().",
            confidence: Confidence.Medium));
    }

    private void CheckStaticMutableField(DiffLine line, List<Finding> findings)
    {
        var content = line.Content.Trim();
        // static field that is not readonly and not a constant
        bool isStaticField = content.Contains("static ", StringComparison.Ordinal) &&
                              !content.Contains("readonly", StringComparison.Ordinal) &&
                              !content.Contains("const ", StringComparison.Ordinal) &&
                              !content.Contains("(") && // not a method
                              !content.Contains("{") && // not a property declaration
                              !content.Contains("=>") && // not an expression-bodied member
                              content.EndsWith(';') &&
                              (content.StartsWith("private ", StringComparison.Ordinal) ||
                               content.StartsWith("public ", StringComparison.Ordinal) ||
                               content.StartsWith("internal ", StringComparison.Ordinal) ||
                               content.StartsWith("protected ", StringComparison.Ordinal));

        if (!isStaticField) return;

        findings.Add(CreateFinding(
            summary: "Static mutable field detected — potential shared state without synchronization.",
            evidence: $"Line {line.LineNumber}: {content}",
            whyItMatters: "Mutable static fields are shared across all threads and requests, requiring explicit synchronization to avoid race conditions.",
            suggestedAction: "Make the field readonly, use Interlocked for simple counters, or prefer instance fields with DI.",
            confidence: Confidence.Medium));
    }
}
