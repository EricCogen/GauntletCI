// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0016, Async Concurrency Risk
/// Detects violations of the async execution contract: async void methods, blocking async
/// calls (.Result / .Wait() / .GetAwaiter().GetResult()), lock(this), and Thread.Sleep
/// in production code.
///
/// Scope: async execution model violations only. Classic thread-safety concerns
/// (static mutable fields, monitor patterns) are out of scope: they produce high FP
/// rates on legitimate patterns (singletons, config caches, type registries) and are
/// better handled by static analysis tools with full type information.
/// </summary>
public class GCI0016_ConcurrencyAndStateRisk : RuleBase
{
    public override string Id => "GCI0016";
    public override string Name => "Async Concurrency Risk";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            bool isTest = WellKnownPatterns.IsTestFile(file.NewPath);

            foreach (var line in file.AddedLines)
            {
                if (WellKnownPatterns.GuardPatterns.IsCommentLine(line.Content)) continue;
                CheckAsyncVoid(line, findings);
                CheckBlockingAsyncCall(line, findings);
                CheckLockThis(line, findings);
                // Thread.Sleep in test code is legitimate timing control.
                if (!isTest) CheckThreadSleepInAsync(line, findings);
            }
        }

        return Task.FromResult(findings);
    }

    private void CheckAsyncVoid(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;
        if (!content.Contains("async void ", StringComparison.Ordinal)) return;

        // Event handlers: (object sender, ...EventArgs ...): legitimate use.
        if (WellKnownPatterns.GuardPatterns.IsEventHandler(content)) return;

        findings.Add(CreateFinding(
            summary: "async void method: exceptions are unobservable and crash the process.",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: "async void methods cannot be awaited. Any exception they throw escapes to AppDomain.UnhandledException and crashes the process. There is no way for the caller to observe or recover from the failure.",
            suggestedAction: "Change the return type to async Task. Only use async void for event handlers where the framework owns the call site and cannot await the result.",
            confidence: Confidence.High));
    }

    private void CheckBlockingAsyncCall(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;

        // .Wait() and .GetAwaiter().GetResult() are unambiguous blocking patterns: always flag.
        if (content.Contains(".Wait()", StringComparison.Ordinal) ||
            content.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal))
        {
            findings.Add(CreateFinding(
                summary: "Blocking async call (.Wait() / .GetAwaiter().GetResult()) risks deadlock.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Blocking on an async operation in a context with a SynchronizationContext (ASP.NET, WPF, Blazor) deadlocks because the continuation needs the thread that is already blocked waiting for it.",
                suggestedAction: "Use await. If sync-over-async is unavoidable, ensure every await in the call chain uses ConfigureAwait(false) to avoid capturing the SynchronizationContext.",
                confidence: Confidence.High));
            return;
        }

        // .Result is ambiguous: it is a Task property but also a common domain property name
        // (HttpResult, OperationResult, ValidationResult, etc.). Only flag when the expression
        // clearly operates on an async result: i.e., .Result is chained directly on a method
        // call (preceded by ')') or the left-hand expression contains explicit Task/Async context.
        if (!content.Contains(".Result", StringComparison.Ordinal)) return;

        var resultIdx = content.IndexOf(".Result", StringComparison.Ordinal);
        if (resultIdx <= 0) return;

        // Skip if the expression is already awaited.
        var beforeResult = content[..resultIdx];
        if (beforeResult.Contains("await ", StringComparison.Ordinal)) return;

        bool isChainedOnCall = content[resultIdx - 1] == ')';
        bool hasTaskContext  = beforeResult.Contains("Async(", StringComparison.Ordinal) ||
                               beforeResult.Contains("Task.", StringComparison.Ordinal) ||
                               beforeResult.Contains("Task<", StringComparison.Ordinal);

        if (!isChainedOnCall && !hasTaskContext) return;

        findings.Add(CreateFinding(
            summary: "Blocking async call (.Result) risks deadlock.",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: "Accessing .Result on a Task blocks the calling thread. In ASP.NET or UI contexts this deadlocks because the continuation requires the synchronization context thread that is already blocked.",
            suggestedAction: "Use await instead of .Result.",
            confidence: Confidence.High));
    }

    private void CheckLockThis(DiffLine line, List<Finding> findings)
    {
        if (!line.Content.Contains("lock(this)", StringComparison.Ordinal) &&
            !line.Content.Contains("lock (this)", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(
            summary: "lock(this) antipattern: the lock object is visible to external callers.",
            evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
            whyItMatters: "Locking on 'this' makes the monitor object publicly visible. Any code holding a reference to this instance can acquire the same lock, creating an external deadlock vector.",
            suggestedAction: "Use a dedicated private readonly object: private readonly object _lock = new();",
            confidence: Confidence.Medium));
    }

    private void CheckThreadSleepInAsync(DiffLine line, List<Finding> findings)
    {
        if (!line.Content.Contains("Thread.Sleep(", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(
            summary: "Thread.Sleep() blocks a thread pool thread.",
            evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
            whyItMatters: "Thread.Sleep blocks the underlying OS thread for the duration of the sleep. In async services this wastes a thread pool thread and degrades throughput under load.",
            suggestedAction: "Replace Thread.Sleep() with await Task.Delay() to yield the thread during the wait.",
            confidence: Confidence.Medium));
    }
}
