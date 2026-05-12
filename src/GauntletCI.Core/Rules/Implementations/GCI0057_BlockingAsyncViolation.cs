// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0057, Blocking Async Pattern Violations
/// Detects patterns where synchronous/blocking operations are used where async should be used:
/// - .Result, .Wait(), .GetAwaiter().GetResult() on Task operations (blocks thread, causes deadlock)
/// - Synchronous file I/O in production code (File.ReadAllText instead of ReadAllTextAsync)
/// </summary>
public class GCI0057_BlockingAsyncViolation : RuleBase
{
    public GCI0057_BlockingAsyncViolation(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0057";
    public override string Name => "Blocking Async Pattern Violation";

    private static readonly Regex BlockingResultPattern =
        new(@"\.\s*Result\s*(?:[;\,\)\]])", RegexOptions.Compiled);

    private static readonly Regex BlockingWaitPattern =
        new(@"\.\s*Wait\s*\(\s*(?:\)|[^)]*\))", RegexOptions.Compiled);

    private static readonly Regex BlockingGetResultPattern =
        new(@"\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)", RegexOptions.Compiled);

    private static readonly Regex SyncFileIoPattern =
        new(@"\bFile\.(ReadAllText|ReadAllLines|WriteAllText|WriteAllLines|Copy|ReadAllBytes|WriteAllBytes)\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AsyncMethodPattern =
        new(@"\basync\s+(?:Task|void)", RegexOptions.Compiled);

    private static readonly string[] BlockingAsyncExemptFiles = new[]
    {
        "Program.cs", "Startup.cs", "AssemblyInfo.cs"
    };

    private static readonly Regex ControllerMethodPattern =
        new(@"public\s+(?:async\s+)?(?:Task|IActionResult|void)\s+\w+\s*\(",
            RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        var diff = context.Diff;

        foreach (var file in diff.Files)
        {
            // Skip test files
            if (WellKnownPatterns.IsTestFile(file.NewPath))
                continue;

            CheckBlockingAsyncCalls(file, findings);
            CheckSyncFileIo(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckBlockingAsyncCalls(DiffFile file, List<Finding> findings)
    {
        // Pattern A: .Result, .Wait(), .GetAwaiter().GetResult() on Task operations
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;

            // Skip string literals and comments
            if (IsInStringOrComment(content))
                continue;

            // Check for .Result pattern
            if (BlockingResultPattern.IsMatch(content))
            {
                // High confidence if it looks like an async method call chain
                if (content.Contains("Async") || content.Contains("Task"))
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: "Blocking call on async operation via .Result",
                        evidence: $"Line {line.LineNumber}: {content.Trim()}",
                        whyItMatters: ".Result blocks the current thread. In ASP.NET, Blazor, or WPF contexts, this can cause deadlock. The synchronization context needs the blocked thread to execute the continuation.",
                        suggestedAction: "Use await instead of .Result. If blocking is truly necessary, add a code comment explaining why and consider using .GetAwaiter().GetResult() with explicit intent.",
                        confidence: Confidence.High,
                        line: line));
                }
            }

            // Check for .Wait() pattern
            if (BlockingWaitPattern.IsMatch(content))
            {
                if (content.Contains("Async") || content.Contains("Task"))
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: "Blocking call on async operation via .Wait()",
                        evidence: $"Line {line.LineNumber}: {content.Trim()}",
                        whyItMatters: ".Wait() blocks the current thread. In async contexts this can cause deadlock.",
                        suggestedAction: "Use await instead of .Wait().",
                        confidence: Confidence.High,
                        line: line));
                }
            }

            // Check for .GetAwaiter().GetResult() pattern
            if (BlockingGetResultPattern.IsMatch(content))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: "Blocking call on async operation via .GetAwaiter().GetResult()",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: ".GetAwaiter().GetResult() blocks the current thread and can cause deadlock in async contexts.",
                    suggestedAction: "Use await instead of .GetAwaiter().GetResult(). Only use this pattern in specific scenarios where blocking is unavoidable, with explicit justification.",
                    confidence: Confidence.High,
                    line: line));
            }
        }
    }

    private void CheckSyncFileIo(DiffFile file, List<Finding> findings)
    {
        // Skip infrastructure files where some blocking I/O is acceptable
        var fileName = Path.GetFileName(file.NewPath);
        if (BlockingAsyncExemptFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            return;

        // Pattern B: Synchronous file I/O
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        foreach (var line in file.AddedLines)
        {
            if (!SyncFileIoPattern.IsMatch(line.Content))
                continue;

            // Skip if in string/comment
            if (IsInStringOrComment(line.Content))
                continue;

            var match = SyncFileIoPattern.Match(line.Content);
            if (!match.Success || match.Groups.Count < 2)
                continue;

            var method = match.Groups[1].Value;

            // Determine confidence based on context
            var confidence = DetermineFileIoConfidence(allLines, line);

            // File.Copy has no direct async equivalent, mention streams
            var suggestion = method.Equals("Copy", StringComparison.OrdinalIgnoreCase)
                ? "For large files, consider using Stream.CopyToAsync() instead of File.Copy()."
                : $"Use await File.{method}Async(...) instead.";

            findings.Add(CreateFinding(
                file,
                summary: $"Synchronous file I/O: {method}() blocks the current thread",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: $"File.{method}() is synchronous and blocks the current thread. Use the async variant to avoid blocking I/O operations.",
                suggestedAction: suggestion,
                confidence: confidence,
                line: line));
        }
    }

    private static Confidence DetermineFileIoConfidence(List<DiffLine> allLines, DiffLine targetLine)
    {
        // Find the index of the target line by content comparison (safer than reference equality)
        var lineIndex = -1;
        for (int idx = 0; idx < allLines.Count; idx++)
        {
            if (allLines[idx].LineNumber == targetLine.LineNumber && allLines[idx].Content == targetLine.Content)
            {
                lineIndex = idx;
                break;
            }
        }

        if (lineIndex < 0)
            return Confidence.Medium; // Conservative default if line not found

        // Look backwards to see if we're in an async method (30-line window)
        for (int i = lineIndex; i >= 0 && i >= lineIndex - 30; i--)
        {
            var prevLine = allLines[i].Content;
            if (AsyncMethodPattern.IsMatch(prevLine))
                return Confidence.High;
            if (prevLine.Contains("public") || prevLine.Contains("private"))
                break; // Reached method boundary
        }

        // In non-async context, it's still problematic but lower confidence
        return Confidence.Medium;
    }

    private static bool IsInStringOrComment(string line)
    {
        // Simple heuristic: skip if line is a comment
        var trimmed = line.Trim();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
            return true;

        // Check if pattern appears before first string literal
        // This is simplified and may have false negatives
        var quoteIndex = line.IndexOf('"');
        if (quoteIndex == -1)
            return false; // No quotes, pattern is likely code

        // If pattern appears after a quote, it might be in a string
        // This is a heuristic and imperfect
        return false;
    }
}
