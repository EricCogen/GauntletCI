// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for exception handling violations:
/// GCI0032 - Empty/swallowed exception handlers
/// GCI0042 - Stubs and NotImplementedException
/// </summary>
public sealed class ExceptionHandlingPatternStrategy : IInferenceStrategy
{
    public IReadOnlySet<string> RuleIds => new HashSet<string> { "GCI0032", "GCI0042" };

    /// <summary>
    /// Applies GCI0032 heuristics: empty or comment-only catch blocks.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        // GCI0032: Empty or comment-only catch blocks
        if (HasEmptyCatch(context.AddedLines))
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0032",
                ShouldTrigger = true,
                ExpectedConfidence = 0.65,
                Reason = "Diff contains an empty or comment-only catch block on added lines",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0042: Stubs and NotImplementedException
        bool hasNotImplemented = context.AddedLines.Any(l =>
            l.Contains("NotImplementedException", StringComparison.Ordinal) ||
            (l.Contains("throw new", StringComparison.Ordinal) && l.Contains("NotImplemented", StringComparison.Ordinal)));

        bool hasStubComment = context.AddedLines.Any(l =>
            l.Contains("TODO", StringComparison.Ordinal) && l.Contains("implement", StringComparison.OrdinalIgnoreCase));

        if (hasNotImplemented || hasStubComment)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0042",
                ShouldTrigger = true,
                ExpectedConfidence = 0.60,
                Reason = "Diff contains stub code with NotImplementedException or TODO implement comment",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }

    /// <summary>
    /// Returns true if the added lines contain an empty or comment-only catch block.
    /// Pattern: catch keyword followed by optional braces containing only comments.
    /// </summary>
    private static bool HasEmptyCatch(IReadOnlyList<string> addedLines)
    {
        for (int i = 0; i < addedLines.Count; i++)
        {
            var line = addedLines[i];
            var trimmed = line.TrimStart();

            // Look for catch block opening
            if (!trimmed.StartsWith("catch", StringComparison.Ordinal))
                continue;

            // Check if brace is on same line
            var openBraceIdx = line.IndexOf('{');
            if (openBraceIdx < 0)
            {
                // Brace might be on next line
                if (i + 1 < addedLines.Count && addedLines[i + 1].TrimStart().StartsWith("{"))
                    openBraceIdx = addedLines[i + 1].IndexOf('{');
                else
                    continue;
            }

            // Find the closing brace
            int braceDepth = 1;
            for (int j = i; j < addedLines.Count && braceDepth > 0; j++)
            {
                var searchLine = j == i ? line[(openBraceIdx + 1)..] : addedLines[j];
                foreach (var ch in searchLine)
                {
                    if (ch == '{') braceDepth++;
                    if (ch == '}') braceDepth--;
                }
            }

            // The catch block appears empty or comment-only
            // Simple heuristic: if the catch keyword is immediately followed by { with only whitespace/comments between,
            // it's likely empty. More sophisticated parsing would require a full C# parser.
            var catchIdx = line.IndexOf("catch");
            var afterCatch = catchIdx >= 0 ? line[catchIdx..].TrimStart() : string.Empty;
            if (afterCatch.StartsWith("{ }", StringComparison.Ordinal) ||
                afterCatch.StartsWith("{}"))
                return true;
        }

        return false;
    }
}
