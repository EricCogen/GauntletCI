// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0008 – Complexity Control
/// Detects excessive nesting, long methods, and duplicate logic.
/// </summary>
public class GCI0008_ComplexityControl : RuleBase
{
    public override string Id => "GCI0008";
    public override string Name => "Complexity Control";

    private const int MaxNestingDepth = 4;
    private const int MaxMethodLines = 30;
    private const int DuplicateLineThreshold = 3;

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckNestingDepth(diff, findings);
        CheckLongMethods(diff, findings);
        CheckDuplicateLogic(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckNestingDepth(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            int depth = 0;
            int maxDepth = 0;
            int maxDepthLine = 0;

            foreach (var line in file.AddedLines)
            {
                foreach (char c in line.Content)
                {
                    if (c == '{') depth++;
                    else if (c == '}') depth = Math.Max(0, depth - 1);
                }

                if (depth > maxDepth)
                {
                    maxDepth = depth;
                    maxDepthLine = line.LineNumber;
                }
            }

            if (maxDepth > MaxNestingDepth)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Nesting depth of {maxDepth} exceeds limit of {MaxNestingDepth} in {file.NewPath}",
                    evidence: $"Max nesting depth reached at line {maxDepthLine}",
                    whyItMatters: "Deep nesting makes code hard to read, test, and maintain. It often indicates missing abstractions.",
                    suggestedAction: "Extract nested logic into private helper methods or use early-return guard clauses.",
                    confidence: Confidence.Low));
            }
        }
    }

    private void CheckLongMethods(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();
            int methodStart = -1;
            int depth = 0;
            int methodLineCount = 0;

            foreach (var line in addedLines)
            {
                bool hasOpen = line.Content.Contains('{');
                bool hasClose = line.Content.Contains('}');

                if (depth == 0 && hasOpen)
                {
                    methodStart = line.LineNumber;
                    methodLineCount = 0;
                }

                if (depth > 0) methodLineCount++;

                if (hasOpen) depth++;
                if (hasClose) depth = Math.Max(0, depth - 1);

                if (depth == 0 && methodStart >= 0 && methodLineCount > MaxMethodLines)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Large method block with {methodLineCount} added lines in {file.NewPath}",
                        evidence: $"Block starting at line {methodStart} has {methodLineCount} added lines",
                        whyItMatters: "Long methods are harder to test, understand, and change without introducing bugs.",
                        suggestedAction: "Decompose the method into smaller, focused helpers.",
                        confidence: Confidence.Low,
                        line: line));
                    methodStart = -1;
                }
            }
        }
    }

    private void CheckDuplicateLogic(DiffContext diff, List<Finding> findings)
    {
        var allAdded = diff.AllAddedLines
            .Select(l => l.Content.Trim())
            .Where(c => c.Length > 10 && !string.IsNullOrWhiteSpace(c))
            .ToList();

        var duplicates = allAdded
            .GroupBy(l => l)
            .Where(g => g.Count() >= DuplicateLineThreshold)
            .Select(g => g.Key)
            .Take(3)
            .ToList();

        if (duplicates.Count > 0)
        {
            findings.Add(CreateFinding(
                summary: $"{duplicates.Count} line(s) appear {DuplicateLineThreshold}+ times in added code.",
                evidence: $"Duplicated: {string.Join(" | ", duplicates.Select(d => d[..Math.Min(60, d.Length)]))}",
                whyItMatters: "Duplicate logic creates maintenance burden — a bug fixed in one copy must be fixed in all.",
                suggestedAction: "Extract the duplicated logic into a shared method or constant.",
                confidence: Confidence.Low));
        }
    }
}
