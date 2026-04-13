// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0011 – Performance Risk
/// Detects common performance anti-patterns in added code.
/// </summary>
public class GCI0011_PerformanceRisk : RuleBase
{
    public override string Id => "GCI0011";
    public override string Name => "Performance Risk";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckPerformanceAntiPatterns(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckPerformanceAntiPatterns(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        // loopStartDepths holds the brace depth at the moment each loop's { was opened.
        // The scope closes when braceDepth drops back to that value on a closing brace.
        var loopStartDepths = new Stack<int>();
        int braceDepth = 0;
        int pendingLoopBrace = 0; // deferred: loop keyword seen but { is on the next line

        for (int i = 0; i < addedLines.Count; i++)
        {
            var content = addedLines[i].Content;
            var trimmed = content.Trim();

            bool isLoopLine = trimmed.StartsWith("for ", StringComparison.Ordinal) ||
                              trimmed.StartsWith("foreach ", StringComparison.Ordinal) ||
                              trimmed.StartsWith("while ", StringComparison.Ordinal);

            int opens  = trimmed.Count(c => c == '{');
            int closes = trimmed.Count(c => c == '}');

            // Process opens first (textual order).
            if (opens > 0)
            {
                if (isLoopLine || pendingLoopBrace > 0)
                {
                    loopStartDepths.Push(braceDepth);
                    if (pendingLoopBrace > 0) pendingLoopBrace--;
                }
                braceDepth += opens;
            }
            else if (isLoopLine)
            {
                // Loop keyword without a same-line { — opening brace is on the next line.
                pendingLoopBrace++;
            }

            // Detection uses depth after opens but before closes.
            int loopDepth = loopStartDepths.Count;

            // .ToList()/.ToArray() inside loop
            if (loopDepth > 0 && (content.Contains(".ToList()", StringComparison.Ordinal) ||
                                   content.Contains(".ToArray()", StringComparison.Ordinal)))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Materializing collection inside loop in {file.NewPath}.",
                    evidence: $"Line {addedLines[i].LineNumber}: {trimmed}",
                    whyItMatters: ".ToList()/.ToArray() inside loops can cause O(n²) allocations.",
                    suggestedAction: "Materialize the collection outside the loop.",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }

            // .Count() instead of .Any()
            if (content.Contains(".Count() > 0", StringComparison.Ordinal) ||
                content.Contains(".Count() == 0", StringComparison.Ordinal) ||
                content.Contains(".Count() >= 1", StringComparison.Ordinal))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Use .Any() instead of .Count() for existence checks in {file.NewPath}.",
                    evidence: $"Line {addedLines[i].LineNumber}: {trimmed}",
                    whyItMatters: ".Count() enumerates the entire collection; .Any() stops at the first element.",
                    suggestedAction: "Replace .Count() > 0 with .Any() and .Count() == 0 with !.Any().",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }

            // new List<>/Dictionary<> inside loops
            if (loopDepth > 0 && (content.Contains("new List<", StringComparison.Ordinal) ||
                                   content.Contains("new Dictionary<", StringComparison.Ordinal)))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Collection allocated inside loop in {file.NewPath}.",
                    evidence: $"Line {addedLines[i].LineNumber}: {trimmed}",
                    whyItMatters: "Allocating collections inside loops increases GC pressure.",
                    suggestedAction: "Move collection allocation outside the loop and clear it between iterations if needed.",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }

            // String concatenation in loops
            if (loopDepth > 0 && content.Contains("+=", StringComparison.Ordinal) &&
                content.Contains('"', StringComparison.Ordinal))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"String concatenation in loop in {file.NewPath}.",
                    evidence: $"Line {addedLines[i].LineNumber}: {trimmed}",
                    whyItMatters: "String += in a loop is O(n²) due to string immutability.",
                    suggestedAction: "Use StringBuilder for string building inside loops.",
                    confidence: Confidence.Medium,
                    line: addedLines[i]));
            }

            // Update brace depth after detection and close exhausted loop scopes.
            for (int j = 0; j < closes; j++)
            {
                braceDepth = Math.Max(0, braceDepth - 1);
                if (loopStartDepths.Count > 0 && braceDepth == loopStartDepths.Peek())
                    loopStartDepths.Pop();
            }
        }
    }
}
