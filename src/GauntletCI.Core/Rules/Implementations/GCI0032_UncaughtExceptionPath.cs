// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0032, Uncaught Exception Path
/// Fires when:
///   1. 'throw new' is added without Assert.Throws or Should().Throw evidence in test files.
///   2. An empty or comment-only catch block is added (silent exception swallowing).
/// Boundary with GCI0042 (TODO/Stub Detection): GCI0042 owns throw new NotImplementedException
/// detection (it is a stub marker, not an exception path risk). Those throws are excluded here
/// to avoid double-reporting.
/// </summary>
public class GCI0032_UncaughtExceptionPath : RuleBase
{
    public override string Id => "GCI0032";
    public override string Name => "Uncaught Exception Path";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        var nonTestFiles = diff.Files
            .Where(f => !f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                        !f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // --- Pattern 1: throw new without test assertions ---
        var nonTestFilesWithThrows = nonTestFiles
            .Where(f => f.AddedLines.Any(l => l.Content.Contains("throw new", StringComparison.Ordinal) &&
                         !l.Content.Contains("throw new NotImplementedException", StringComparison.Ordinal) &&
                         !WellKnownPatterns.ExceptionPatterns.GuardClauseThrows.Any(g => l.Content.Contains(g, StringComparison.Ordinal))))
            .ToList();
        
        if (nonTestFilesWithThrows.Any())
        {
            // Only non-removed lines: a deleted assertion is evidence that coverage was removed, not added.
            var testLines = diff.Files
                .Where(f => f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                            f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => f.Hunks.SelectMany(h => h.Lines))
                .Where(l => l.Kind != DiffLineKind.Removed)
                .Select(l => l.Content)
                .ToList();

            bool hasThrowAssertions = testLines.Any(line =>
                WellKnownPatterns.ExceptionPatterns.ThrowAssertions.Any(assertion => line.Contains(assertion, StringComparison.Ordinal)));

            if (!hasThrowAssertions)
            {
                var throwCount = nonTestFilesWithThrows
                    .SelectMany(f => f.AddedLines)
                    .Count(l => l.Content.Contains("throw new", StringComparison.Ordinal) &&
                               !l.Content.Contains("throw new NotImplementedException", StringComparison.Ordinal) &&
                               !WellKnownPatterns.ExceptionPatterns.GuardClauseThrows.Any(g => l.Content.Contains(g, StringComparison.Ordinal)));
                
                // Attribute to the first file with throws
                findings.Add(CreateFinding(
                    nonTestFilesWithThrows[0],
                    summary: $"{throwCount} 'throw new' statement(s) added without Assert.Throws or Should().Throw evidence in this diff.",
                    evidence: $"{throwCount} added 'throw new' statement(s) in non-test files.",
                    whyItMatters: "New exception paths that are untested may crash callers silently in production when the edge case is reached.",
                    suggestedAction: "Add xUnit `Assert.Throws<T>` or FluentAssertions `.Should().Throw<T>()` tests for each new exception path.",
                    confidence: Confidence.Medium));
            }
        }

        // --- Pattern 2: empty or comment-only catch block (silent swallowing) ---
        var filesWithEmptyCatches = nonTestFiles
            .Where(f => CountEmptyCatchesInFile(f) > 0)
            .ToList();
        
        if (filesWithEmptyCatches.Any())
        {
            var totalEmptyCatches = filesWithEmptyCatches.Sum(f => CountEmptyCatchesInFile(f));
            findings.Add(CreateFinding(
                filesWithEmptyCatches[0],
                summary: $"{totalEmptyCatches} empty or comment-only catch block(s) added, silently swallowing exceptions.",
                evidence: $"{totalEmptyCatches} added catch block(s) in non-test files contain no executable statements.",
                whyItMatters: "An empty catch block discards the exception entirely, hiding failures from callers and making diagnostics impossible.",
                suggestedAction: "Add error handling, logging, or 'throw;' to propagate the exception. Never silently swallow exceptions.",
                confidence: Confidence.High));
        }

        return Task.FromResult(findings);
    }

    // Counts catch blocks in added lines whose bodies contain no executable statements
    // (empty braces, or only whitespace and comments).
    private static int CountEmptyCatchesInFile(DiffFile file)
    {
        var addedLines = file.AddedLines.Select(l => l.Content).ToList();
        return CountEmptyCatchesInLines(addedLines);
    }

    // Counts catch blocks in added lines whose bodies contain no executable statements
    // (empty braces, or only whitespace and comments).
    private static int CountEmptyCatches(IEnumerable<DiffFile> nonTestFiles)
    {
        int count = 0;
        foreach (var file in nonTestFiles)
        {
            var addedLines = file.AddedLines.Select(l => l.Content).ToList();
            count += CountEmptyCatchesInLines(addedLines);
        }
        return count;
    }

    private static int CountEmptyCatchesInLines(List<string> lines)
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (!StartsWithCatchKeyword(trimmed)) continue;

            // Single-line: catch { } or catch (Exception ex) { }
            if (IsSingleLineEmptyCatch(trimmed))
            {
                count++;
                continue;
            }

            // Multi-line: scan the catch block body for non-comment content
            if (IsMultiLineEmptyCatch(lines, i))
                count++;
        }
        return count;
    }

    private static bool StartsWithCatchKeyword(string trimmed)
    {
        // Handle "catch (...)", "} catch (...)", and bare "catch"
        int idx = trimmed.IndexOf("catch", StringComparison.Ordinal);
        if (idx < 0) return false;
        bool prevOk = idx == 0 || (!char.IsLetterOrDigit(trimmed[idx - 1]) && trimmed[idx - 1] != '_');
        int nextIdx = idx + 5;
        bool nextOk = nextIdx >= trimmed.Length || (!char.IsLetterOrDigit(trimmed[nextIdx]) && trimmed[nextIdx] != '_');
        return prevOk && nextOk;
    }

    // Matches: catch { } or catch (Exception ex) { } or catch (SomeType) { /* comment */ }
    private static bool IsSingleLineEmptyCatch(string trimmed)
    {
        int openBrace = trimmed.IndexOf('{');
        int closeBrace = trimmed.LastIndexOf('}');
        if (openBrace < 0 || closeBrace <= openBrace) return false;

        var body = trimmed[(openBrace + 1)..closeBrace].Trim();
        if (body.Length == 0) return true;

        // Body is only a comment
        return body.StartsWith("//") || body.StartsWith("/*") || body.StartsWith("*");
    }

    private static bool IsMultiLineEmptyCatch(List<string> lines, int catchIndex)
    {
        bool inBlock = false;
        bool hasNonCommentContent = false;

        int windowEnd = Math.Min(catchIndex + 10, lines.Count);
        for (int j = catchIndex; j < windowEnd; j++)
        {
            var trimmed = lines[j].Trim();

            if (!inBlock)
            {
                // The catch declaration line opens the block; skip it as content.
                if (trimmed.Contains('{')) inBlock = true;
                continue;
            }

            // Inside the catch block body.
            if (trimmed == "}" || trimmed.Length == 0) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;

            hasNonCommentContent = true;
            break;
        }

        return inBlock && !hasNonCommentContent;
    }
}
