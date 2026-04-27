// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0044 – Performance Hotpath Risk
/// Detects Thread.Sleep, LINQ inside loops, and unbounded collection growth inside loops.
/// </summary>
public class GCI0044_PerformanceHotpathRisk : RuleBase
{
    public override string Id => "GCI0044";
    public override string Name => "Performance Hotpath Risk";

    private static readonly string[] LinqMethods =
        [".Where(", ".Select(", ".FirstOrDefault(", ".Any(", ".Count("];

    private static readonly string[] LoopKeywords =
        ["for (", "foreach (", "while ("];

    // "foreach" is the standard accumulator pattern — only flag for/while unbounded growth
    private static readonly string[] UnboundedLoopKeywords = ["for (", "while ("];

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("spec", StringComparison.OrdinalIgnoreCase);

    // Rule implementation files use LINQ inside analysis loops as standard practice;
    // these are engine internals, not production hotpath code.
    private static bool IsRuleImplementationFile(string path) =>
        path.Contains("Rules/Implementations", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"Rules\Implementations", StringComparison.OrdinalIgnoreCase);

    private static bool HasLinqCall(string content)
    {
        foreach (var m in LinqMethods)
            if (content.Contains(m, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool HasLoopConstruct(string content)
    {
        foreach (var k in LoopKeywords)
            if (content.Contains(k, StringComparison.Ordinal)) return true;
        return false;
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (IsTestFile(file.NewPath)) continue;
            if (IsRuleImplementationFile(file.NewPath)) continue;
            CheckThreadSleep(file, findings);
            CheckLinqInsideLoop(file, findings);
            CheckAddInsideLoop(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckThreadSleep(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            if (!line.Content.Contains("Thread.Sleep(", StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Thread.Sleep detected in production code",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Thread.Sleep blocks the calling thread, wastes resources, and degrades throughput on hotpaths. It is especially harmful in async contexts.",
                suggestedAction: "Replace Thread.Sleep with await Task.Delay(...) in async code, or redesign the flow to avoid polling delays.",
                confidence: Confidence.Medium,
                line: line));
        }
    }

    private void CheckLinqInsideLoop(DiffFile file, List<Finding> findings)
    {
        foreach (var hunk in file.Hunks)
        {
            // Include context lines so loop keywords on unchanged lines are detected
            var nonRemovedLines = new List<DiffLine>();
            foreach (var l in hunk.Lines)
                if (l.Kind != DiffLineKind.Removed) nonRemovedLines.Add(l);

            for (int i = 0; i < nonRemovedLines.Count; i++)
            {
                if (nonRemovedLines[i].Kind != DiffLineKind.Added) continue;
                if (!HasLinqCall(nonRemovedLines[i].Content)) continue;

                int lookbackStart = Math.Max(0, i - 10);
                bool inLoop = false;
                for (int j = lookbackStart; j < i; j++)
                {
                    if (HasLoopConstruct(nonRemovedLines[j].Content))
                    {
                        inLoop = true;
                        break;
                    }
                }

                if (!inLoop) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "LINQ query inside a loop",
                    evidence: $"Line {nonRemovedLines[i].LineNumber}: {nonRemovedLines[i].Content.Trim()}",
                    whyItMatters: "LINQ queries inside loops cause repeated enumeration, yielding O(n²) or worse complexity that degrades performance at scale.",
                    suggestedAction: "Move the LINQ query outside the loop, pre-compute the result into a collection, or use a dictionary/lookup for O(1) access.",
                    confidence: Confidence.Medium,
                    line: nonRemovedLines[i]));
            }
        }
    }

    private void CheckAddInsideLoop(DiffFile file, List<Finding> findings)
    {
        foreach (var hunk in file.Hunks)
        {
            // Use non-removed lines for lookback so loop keywords on context lines are detected
            var nonRemovedLines = new List<DiffLine>();
            foreach (var l in hunk.Lines)
                if (l.Kind != DiffLineKind.Removed) nonRemovedLines.Add(l);

            for (int i = 0; i < nonRemovedLines.Count; i++)
            {
                if (nonRemovedLines[i].Kind != DiffLineKind.Added) continue;
                var content = nonRemovedLines[i].Content;
                if (!content.Contains(".Add(", StringComparison.Ordinal)) continue;
                // Unsafe.Add(ref ...) is SIMD/pointer arithmetic, not a collection mutation.
                // Neutralise it and re-check so mixed lines (Unsafe.Add + real .Add) still fire.
                if (!content.Replace("Unsafe.Add(", "UNSAFE_PTR(")
                            .Contains(".Add(", StringComparison.Ordinal)) continue;

                int lookbackStart = Math.Max(0, i - 10);
                bool inLoop = false;
                for (int j = lookbackStart; j < i; j++)
                {
                    foreach (var k in UnboundedLoopKeywords)
                    {
                        if (nonRemovedLines[j].Content.Contains(k, StringComparison.Ordinal))
                        {
                            // DB reader loops are bounded by query results — not a hotpath risk.
                            if (nonRemovedLines[j].Content.Contains(".Read()", StringComparison.Ordinal))
                                break;
                            inLoop = true;
                            break;
                        }
                    }
                    if (inLoop) break;
                }

                if (!inLoop) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Unbounded collection growth (.Add) inside a loop",
                    evidence: $"Line {nonRemovedLines[i].LineNumber}: {content.Trim()}",
                    whyItMatters: "Repeatedly calling .Add inside a loop on an unbounded collection can exhaust memory if the loop runs over large input or indefinitely.",
                    suggestedAction: "Pre-allocate the collection with a known capacity, or use a streaming pattern (yield return / IAsyncEnumerable) instead of accumulating all results.",
                    confidence: Confidence.Medium,
                    line: nonRemovedLines[i]));
            }
        }
    }
}
