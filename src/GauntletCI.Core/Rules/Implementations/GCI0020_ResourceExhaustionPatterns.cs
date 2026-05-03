// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0020, Resource Exhaustion Pattern Detection
/// Detects common patterns that lead to resource exhaustion vulnerabilities:
/// - Timeout removal, MaxIterations removal
/// - Buffer/pool size increases without bounds
/// - Unbounded operation additions
/// - Resource cleanup removal
/// - Async operations without limits
/// </summary>
public class GCI0020_ResourceExhaustionPatterns : RuleBase
{
    public GCI0020_ResourceExhaustionPatterns(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0020";
    public override string Name => "Resource Exhaustion Pattern Detection";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckTimeoutRemoval(diff, findings);
        CheckIterationLimitRemoval(diff, findings);
        CheckResourceLimitIncrease(diff, findings);
        CheckResourceCleanupRemoval(diff, findings);
        CheckAsyncOperationWithoutLimit(diff, findings);

        return Task.FromResult(findings);
    }

    /// <summary>PATTERN 1: Timeout or deadline removed (80% confidence).</summary>
    private void CheckTimeoutRemoval(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath))
                continue;

            // Look for removed timeout-related lines
            var removedTimeouts = file.RemovedLines
                .Where(l => WellKnownPatterns.TimeoutPatterns.Any(p => 
                    l.Content.Contains(p, StringComparison.OrdinalIgnoreCase) &&
                    (l.Content.Contains("=", StringComparison.Ordinal) || 
                     l.Content.Contains("(", StringComparison.Ordinal))))
                .ToList();

            if (removedTimeouts.Count == 0) continue;

            // Check if timeout is replaced with MaxValue or removed without replacement
            var hasTimeoutInAdded = file.AddedLines
                .Any(l => WellKnownPatterns.TimeoutPatterns.Any(p => 
                    l.Content.Contains(p, StringComparison.OrdinalIgnoreCase)));

            if (!hasTimeoutInAdded)
            {
                var example = removedTimeouts.First().Content.Trim();
                findings.Add(CreateFinding(
                    file,
                    summary: "Timeout or deadline removed without replacement - potential resource exhaustion vulnerability.",
                    evidence: $"Removed: {example}",
                    whyItMatters: "Operations without timeouts can hang indefinitely, exhausting system resources and causing denial of service.",
                    suggestedAction: "Add timeout/deadline protection back or verify the operation is now bounded by other means.",
                    confidence: Confidence.High,
                    removedTimeouts.First()));
            }
        }
    }

    /// <summary>PATTERN 2: Iteration limit removed (80% confidence).</summary>
    private void CheckIterationLimitRemoval(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath))
                continue;

            var removedLimits = file.RemovedLines
                .Where(l => WellKnownPatterns.IterationLimitPatterns.Any(p =>
                    l.Content.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (removedLimits.Count == 0) continue;

            var hasIterationLimitInAdded = file.AddedLines
                .Any(l => WellKnownPatterns.IterationLimitPatterns.Any(p =>
                    l.Content.Contains(p, StringComparison.OrdinalIgnoreCase)));

            if (!hasIterationLimitInAdded)
            {
                var example = removedLimits.First().Content.Trim();
                findings.Add(CreateFinding(
                    file,
                    summary: "Iteration limit removed - potential infinite loop vulnerability.",
                    evidence: $"Removed: {example}",
                    whyItMatters: "Loops without iteration limits can spin indefinitely, consuming CPU and causing denial of service. CVE-2019-0981 is an example.",
                    suggestedAction: "Re-add iteration limit or add loop termination conditions.",
                    confidence: Confidence.High,
                    removedLimits.First()));
            }
        }
    }

    /// <summary>PATTERN 3: Resource allocation increased significantly (70% confidence).</summary>
    private void CheckResourceLimitIncrease(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath))
                continue;

            foreach (var removedLine in file.RemovedLines)
            {
                // Match patterns like "MAX_CONNECTIONS = 1000"
                if (!RemovalHasResourceAllocation(removedLine)) continue;

                var oldValue = ExtractNumericValue(removedLine.Content);
                if (oldValue < 100) continue; // Only flag significant increases

                // Find corresponding added line
                var addedLine = file.AddedLines
                    .FirstOrDefault(l => HaveSameName(removedLine.Content, l.Content) &&
                                         ExtractNumericValue(l.Content) > oldValue * 5); // 5x increase

                if (addedLine != null)
                {
                    var newValue = ExtractNumericValue(addedLine.Content);
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Resource limit increased significantly ({oldValue} → {newValue}).",
                        evidence: $"Before: {removedLine.Content.Trim()} | After: {addedLine.Content.Trim()}",
                        whyItMatters: "Large increases in resource limits (connections, threads, memory) can enable resource exhaustion attacks.",
                        suggestedAction: "Verify the increase is intentional and review resource management logic.",
                        confidence: Confidence.Medium,
                        addedLine));
                }
            }
        }
    }

    /// <summary>PATTERN 4: Resource cleanup removed (75% confidence).</summary>
    private void CheckResourceCleanupRemoval(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath))
                continue;

            var removedCleanup = file.RemovedLines
                .Where(l => WellKnownPatterns.ResourceCleanupPatterns.Any(p =>
                    l.Content.Contains(p, StringComparison.Ordinal)))
                .ToList();

            if (removedCleanup.Count == 0) continue;

            // Check if cleanup is added back in a different form
            var hasCleanupInAdded = file.AddedLines
                .Any(l => WellKnownPatterns.ResourceCleanupPatterns.Any(p =>
                    l.Content.Contains(p, StringComparison.Ordinal)));

            if (!hasCleanupInAdded)
            {
                var example = removedCleanup.First().Content.Trim();
                findings.Add(CreateFinding(
                    file,
                    summary: "Resource cleanup (using/Dispose) removed - potential resource leak.",
                    evidence: $"Removed: {example}",
                    whyItMatters: "Without proper cleanup, resources like connections and file handles accumulate, leading to exhaustion.",
                    suggestedAction: "Restore cleanup logic or use try-finally to ensure resources are released.",
                    confidence: Confidence.High,
                    removedCleanup.First()));
            }
        }
    }

    /// <summary>PATTERN 5: Async operation spawned (65% confidence).</summary>
    private void CheckAsyncOperationWithoutLimit(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath))
                continue;

            foreach (var addedLine in file.AddedLines)
            {
                // Look for Task.Run or Task.Factory patterns being added
                if (!addedLine.Content.Contains("Task.Run", StringComparison.Ordinal) &&
                    !addedLine.Content.Contains("Task.Factory", StringComparison.Ordinal))
                    continue;

                // Phase 16 Guards: fire-and-forget background tasks and instance-scoped caches
                if (WellKnownPatterns.IsIntentionalBackgroundTask(addedLine.Content)) continue;
                if (WellKnownPatterns.IsInstanceScopedCache(addedLine.Content)) continue;

                // Flag any new Task.Run/Factory as potential unbounded concurrency
                findings.Add(CreateFinding(
                    file,
                    summary: "New async task spawned - verify concurrency limits.",
                    evidence: $"Added: {addedLine.Content.Trim()}",
                    whyItMatters: "Creating unlimited tasks can exhaust the thread pool, leading to denial of service.",
                    suggestedAction: "Use SemaphoreSlim or similar to limit concurrent operations.",
                    confidence: Confidence.Medium,
                    addedLine));
            }
        }
    }

    private static bool RemovalHasResourceAllocation(DiffLine line)
    {
        return WellKnownPatterns.ResourceLimitPatterns.Any(p =>
            line.Content.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
            line.Content.Contains("=", StringComparison.Ordinal);
    }

    private static int ExtractNumericValue(string content)
    {
        var match = Regex.Match(content, @"\b(\d+)\b");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static bool HaveSameName(string line1, string line2)
    {
        // Extract variable/constant name (before the =)
        var name1 = line1.Split('=')[0].Trim().Split(' ').Last();
        var name2 = line2.Split('=')[0].Trim().Split(' ').Last();
        return name1 == name2;
    }
}

