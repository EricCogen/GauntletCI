// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0016, Async Concurrency Risk
/// Detects violations of the async execution contract: async void methods, blocking async
/// calls (.Result / .Wait() / .GetAwaiter().GetResult()), lock(this), and Thread.Sleep
/// in production code.
/// </summary>
public class GCI0016_ConcurrencyAndStateRisk : RuleBase
{
    public GCI0016_ConcurrencyAndStateRisk(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0016";
    public override string Name => "Async Concurrency Risk";

    private static readonly Regex AsyncVoidMethodRegex = new(
        @"\basync\s+void\s+\w+\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
                CheckAsyncVoid(file, line, findings);
                CheckBlockingAsyncCall(file, line, findings);
                CheckLockThis(file, line, findings);
                if (!isTest) CheckThreadSleepInAsync(file, line, findings);
            }
        }

        return Task.FromResult(findings);
    }

    private void CheckAsyncVoid(DiffFile file, DiffLine line, List<Finding> findings)
    {
        var content = WellKnownPatterns.GuardPatterns.ForPatternScan(line.Content);
        if (!AsyncVoidMethodRegex.IsMatch(content)) return;
        if (WellKnownPatterns.GuardPatterns.IsEventHandler(content)) return;

        findings.Add(CreateFinding(file,
            summary: "async void method: exceptions are unobservable and crash the process.",
            evidence: line.Content.Trim(),
            whyItMatters: "async void methods cannot be awaited. Any exception they throw escapes to AppDomain.UnhandledException and crashes the process. There is no way for the caller to observe or recover from the failure.",
            suggestedAction: "Change the return type to async Task. Only use async void for event handlers where the framework owns the call site and cannot await the result.",
            confidence: Confidence.High,
            line: line));
    }

    private void CheckBlockingAsyncCall(DiffFile file, DiffLine line, List<Finding> findings)
    {
        var content = WellKnownPatterns.GuardPatterns.ForPatternScan(line.Content);

        if (WellKnownPatterns.HasDevOnlyMarker(content)) return;
        if (IsLegitimateAsyncPattern(content)) return;
        if (WellKnownPatterns.IsOrmAsyncPattern(content)) return;
        if (WellKnownPatterns.IsBoundedSynchronization(content)) return;

        bool isUnboundedBlocking = WellKnownPatterns.IsBlockingAsyncWithoutTimeout(content);
        bool hasWait = content.Contains(".Wait()", StringComparison.Ordinal);
        bool hasGetAwaiterGetResult = content.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal) ||
                                      Regex.IsMatch(content, @"\.ConfigureAwait\s*\([^)]*\)\s*\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)", RegexOptions.IgnoreCase);

        if (hasWait || hasGetAwaiterGetResult)
        {
            findings.Add(CreateFinding(file,
                summary: isUnboundedBlocking
                    ? "Blocking async call (.Wait() / .GetAwaiter().GetResult()) without timeout - deadlock + resource exhaustion risk."
                    : "Blocking async call (.Wait() / .GetAwaiter().GetResult()) risks deadlock.",
                evidence: line.Content.Trim(),
                whyItMatters: "Blocking on an async operation in a context with a SynchronizationContext (ASP.NET, WPF, Blazor) deadlocks because the continuation needs the thread that is already blocked waiting for it." +
                    (isUnboundedBlocking ? " Combined with missing timeout, this can exhaust system resources and cause DoS." : ""),
                suggestedAction: "Use await. If sync-over-async is unavoidable, ensure every await in the call chain uses ConfigureAwait(false) to avoid capturing the SynchronizationContext. " +
                    (isUnboundedBlocking ? "Add CancellationToken or TimeSpan timeout protection." : ""),
                confidence: Confidence.High,
                line: line));
            return;
        }

        if (!content.Contains(".Result", StringComparison.Ordinal)) return;

        var resultIdx = content.IndexOf(".Result", StringComparison.Ordinal);
        if (resultIdx <= 0) return;

        var beforeResult = content[..resultIdx];
        if (beforeResult.Contains("await ", StringComparison.Ordinal)) return;

        bool isChainedOnCall = content[resultIdx - 1] == ')';
        bool hasTaskContext = beforeResult.Contains("Async(", StringComparison.Ordinal) ||
                               beforeResult.Contains("Task.", StringComparison.Ordinal) ||
                               beforeResult.Contains("Task<", StringComparison.Ordinal);

        if (!isChainedOnCall && !hasTaskContext) return;

        findings.Add(CreateFinding(file,
            summary: isUnboundedBlocking
                ? "Blocking async call (.Result) without timeout - deadlock + resource exhaustion risk."
                : "Blocking async call (.Result) risks deadlock.",
            evidence: line.Content.Trim(),
            whyItMatters: "Accessing .Result on a Task blocks the calling thread. In ASP.NET or UI contexts this deadlocks because the continuation requires the synchronization context thread that is already blocked." +
                (isUnboundedBlocking ? " Combined with missing timeout, this can exhaust system resources and cause DoS." : ""),
            suggestedAction: "Use await instead of .Result." +
                (isUnboundedBlocking ? " Add CancellationToken or TimeSpan timeout protection." : ""),
            confidence: Confidence.High,
            line: line));
    }

    private void CheckLockThis(DiffFile file, DiffLine line, List<Finding> findings)
    {
        var content = WellKnownPatterns.GuardPatterns.ForPatternScan(line.Content);
        if (!content.Contains("lock(this)", StringComparison.Ordinal) &&
            !content.Contains("lock (this)", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(file,
            summary: "lock(this) antipattern: the lock object is visible to external callers.",
            evidence: line.Content.Trim(),
            whyItMatters: "Locking on 'this' makes the monitor object publicly visible. Any code holding a reference to this instance can acquire the same lock, creating an external deadlock vector.",
            suggestedAction: "Use a dedicated private readonly object: private readonly object _lock = new();",
            confidence: Confidence.Medium,
            line: line));
    }

    private void CheckThreadSleepInAsync(DiffFile file, DiffLine line, List<Finding> findings)
    {
        var content = WellKnownPatterns.GuardPatterns.ForPatternScan(line.Content);
        if (!content.Contains("Thread.Sleep(", StringComparison.Ordinal)) return;

        findings.Add(CreateFinding(file,
            summary: "Thread.Sleep() blocks a thread pool thread.",
            evidence: line.Content.Trim(),
            whyItMatters: "Thread.Sleep blocks the underlying OS thread for the duration of the sleep. In async services this wastes a thread pool thread and degrades throughput under load.",
            suggestedAction: "Replace Thread.Sleep() with await Task.Delay() to yield the thread during the wait.",
            confidence: Confidence.Medium,
            line: line));
    }

    private static bool IsLegitimateAsyncPattern(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;

        bool hasBlocking = content.Contains(".Wait()", StringComparison.Ordinal) ||
                           content.Contains(".GetAwaiter().GetResult()", StringComparison.OrdinalIgnoreCase) ||
                           Regex.IsMatch(content, @"\.ConfigureAwait\s*\([^)]*\)\s*\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)", RegexOptions.IgnoreCase) ||
                           (content.Contains(".Result", StringComparison.Ordinal) &&
                            !content.Contains("await ", StringComparison.Ordinal));

        // Fire-and-forget only when the line does not also block.
        if (!hasBlocking &&
            content.Contains("_ =", StringComparison.Ordinal) &&
            (content.Contains("Task", StringComparison.Ordinal) ||
             content.Contains("Async(", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (content.Contains("ConfigureAwait(false)", StringComparison.OrdinalIgnoreCase) && !hasBlocking)
            return true;

        const string taskRunCall = "Task" + ".Run(";
        if (!hasBlocking &&
            ((content.Contains(taskRunCall, StringComparison.OrdinalIgnoreCase) &&
              !content.Contains(".Result", StringComparison.OrdinalIgnoreCase) &&
              !content.Contains(".Wait(", StringComparison.OrdinalIgnoreCase)) ||
             content.Contains("ThreadPool.QueueUserWorkItem", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (content.Contains("intentional", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("fire-and-forget", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("by design", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
