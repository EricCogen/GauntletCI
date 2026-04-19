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

    // Unbounded loops: for/while can grow without limit. foreach is always bounded by its source.
    private static readonly string[] UnboundedLoopKeywords =
        ["for (", "while ("];

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("spec", StringComparison.OrdinalIgnoreCase);

    private static bool HasLinqCall(string content) =>
        LinqMethods.Any(m => content.Contains(m, StringComparison.Ordinal));

    private static bool HasLoopConstruct(string content) =>
        LoopKeywords.Any(k => content.Contains(k, StringComparison.Ordinal));

    private static bool HasUnboundedLoopConstruct(string content) =>
        UnboundedLoopKeywords.Any(k => content.Contains(k, StringComparison.Ordinal));

    /// <summary>
    /// Extracts the loop variable name from a foreach statement, e.g. "foreach (var hunk in file.Hunks)"
    /// returns "hunk". Returns null for non-foreach constructs.
    /// </summary>
    private static string? ExtractForeachVariable(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("foreach (", StringComparison.Ordinal) &&
            !trimmed.StartsWith("foreach(", StringComparison.Ordinal)) return null;
        // Pattern: foreach (var/Type name in ...)
        var parenStart = trimmed.IndexOf('(');
        if (parenStart < 0) return null;
        var inner = trimmed[(parenStart + 1)..].TrimStart();
        // Skip "var " or type name
        int spaceAfterType = inner.IndexOf(' ');
        if (spaceAfterType < 0) return null;
        inner = inner[(spaceAfterType + 1)..].TrimStart();
        // Next token is the variable name, terminated by space or ')'
        int endOfVar = inner.IndexOfAny([' ', ')']);
        return endOfVar > 0 ? inner[..endOfVar] : null;
    }

    /// <summary>
    /// Returns true when the LINQ call's receiver starts with the loop variable (or its member),
    /// meaning the LINQ is bounded filtering of the current iteration's data.
    /// e.g. "hunk.Lines.Where(...)" with loopVar="hunk" → true (skip: bounded)
    /// e.g. "allItems.Where(i => order.Id)" with loopVar="order" → false (flag: O(n²))
    /// </summary>
    private static bool LinqIsOnLoopVariable(string content, string loopVar)
    {
        foreach (var method in LinqMethods)
        {
            int idx = content.IndexOf(method, StringComparison.Ordinal);
            if (idx <= 0) continue;
            var receiver = content[..idx].TrimStart();
            if (receiver.StartsWith(loopVar + ".", StringComparison.Ordinal) ||
                receiver.Equals(loopVar, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files.Where(f => !IsTestFile(f.NewPath)))
        {
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
            var addedLines = hunk.Lines
                .Where(l => l.Kind == DiffLineKind.Added)
                .ToList();

            for (int i = 0; i < addedLines.Count; i++)
            {
                if (!HasLinqCall(addedLines[i].Content)) continue;

                int lookbackStart = Math.Max(0, i - 10);
                bool inLoop = false;
                string? loopVar = null;
                for (int j = lookbackStart; j < i; j++)
                {
                    if (HasLoopConstruct(addedLines[j].Content))
                    {
                        inLoop = true;
                        loopVar = ExtractForeachVariable(addedLines[j].Content);
                        break;
                    }
                }

                if (!inLoop) continue;

                // Skip when the LINQ is called directly on the loop variable's own members —
                // that's bounded filtering of the current iteration's data, not O(n²).
                if (loopVar is not null && LinqIsOnLoopVariable(addedLines[i].Content, loopVar))
                    continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "LINQ query inside a loop",
                    evidence: $"Line {addedLines[i].LineNumber}: {addedLines[i].Content.Trim()}",
                    whyItMatters: "LINQ queries inside loops cause repeated enumeration, yielding O(n²) or worse complexity that degrades performance at scale.",
                    suggestedAction: "Move the LINQ query outside the loop, pre-compute the result into a collection, or use a dictionary/lookup for O(1) access.",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }
        }
    }

    private void CheckAddInsideLoop(DiffFile file, List<Finding> findings)
    {
        foreach (var hunk in file.Hunks)
        {
            var addedLines = hunk.Lines
                .Where(l => l.Kind == DiffLineKind.Added)
                .ToList();

            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                if (!content.Contains(".Add(", StringComparison.Ordinal)) continue;

                int lookbackStart = Math.Max(0, i - 10);
                bool inUnboundedLoop = false;
                for (int j = lookbackStart; j < i; j++)
                {
                    if (HasUnboundedLoopConstruct(addedLines[j].Content))
                    {
                        inUnboundedLoop = true;
                        break;
                    }
                }

                // Only flag unbounded loops (for/while). foreach is bounded by its source enumerable.
                if (!inUnboundedLoop) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Unbounded collection growth (.Add) inside a loop",
                    evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                    whyItMatters: "Repeatedly calling .Add inside a loop on an unbounded collection can exhaust memory if the loop runs over large input or indefinitely.",
                    suggestedAction: "Pre-allocate the collection with a known capacity, or use a streaming pattern (yield return / IAsyncEnumerable) instead of accumulating all results.",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }
        }
    }
}
