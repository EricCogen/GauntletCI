// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0009 – Consistency with Patterns
/// Flags methods named *Async that don't return Task/ValueTask, synchronous methods
/// with async-convention names added to async codebases, and .ToLower()/.ToUpper()
/// used in string comparisons.
/// </summary>
[ArchivedRule("Requires full repo context to answer; unanswerable from diff alone")]
public class GCI0009_ConsistencyWithPatterns : RuleBase
{
    public override string Id => "GCI0009";
    public override string Name => "Consistency with Patterns";

    private static readonly string[] AsyncSoundingPrefixes =
    [
        "Get", "Fetch", "Load", "Save", "Send", "Delete", "Create", "Update",
        "Read", "Write", "Execute", "Process", "Handle", "Post", "Put", "Patch",
        "Query", "Find", "Search", "Invoke", "Submit", "Publish", "Dispatch"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();
            bool fileUsesAsync = allLines.Any(l =>
                l.Content.Contains("async ", StringComparison.Ordinal) ||
                l.Content.Contains("await ", StringComparison.Ordinal) ||
                l.Content.Contains("Task<", StringComparison.Ordinal) ||
                l.Content.Contains("ValueTask", StringComparison.Ordinal));

            foreach (var line in file.AddedLines)
            {
                if (line.Content.TrimStart().StartsWith("//")) continue;
                CheckAsyncNamingMismatch(line, file, fileUsesAsync, findings);
                CheckStringComparisonAntiPattern(line, file, findings);
            }
        }

        return Task.FromResult(findings);
    }

    private void CheckAsyncNamingMismatch(DiffLine line, DiffFile file, bool fileUsesAsync, List<Finding> findings)
    {
        var content = line.Content.Trim();
        if (!IsMethodDeclaration(content)) return;

        bool returnsTask = content.Contains("Task<", StringComparison.Ordinal) ||
                           content.Contains("Task ", StringComparison.Ordinal) ||
                           content.Contains("ValueTask", StringComparison.Ordinal);
        bool isAsync = content.Contains("async ", StringComparison.Ordinal);

        // High confidence: method name ends with Async but isn't awaitable
        if (content.Contains("Async(", StringComparison.Ordinal) && !returnsTask && !isAsync)
        {
            findings.Add(CreateFinding(file,
                summary: $"Method named *Async does not return Task or use async in {file.NewPath}.",
                evidence: $"Line {line.LineNumber}: {content}",
                whyItMatters: "Methods suffixed with 'Async' must be awaitable by convention. A synchronous *Async method breaks caller expectations and can cause silent deadlocks if callers add .Result.",
                suggestedAction: "Return Task<T> or ValueTask<T> and mark the method async, or remove the Async suffix if the method is intentionally synchronous.",
                confidence: Confidence.High,
                line: line));
            return;
        }

        // Low confidence: sync method with async-sounding name in an async codebase
        if (!fileUsesAsync || returnsTask || isAsync) return;

        string methodName = ExtractMethodName(content);
        if (methodName.Length == 0) return;

        bool hasAsyncSoundingName = AsyncSoundingPrefixes.Any(prefix =>
            methodName.StartsWith(prefix, StringComparison.Ordinal) &&
            methodName.Length > prefix.Length);

        if (!hasAsyncSoundingName) return;

        findings.Add(CreateFinding(file,
            summary: $"Synchronous method '{methodName}' added to a codebase that uses async/await in {file.NewPath}.",
            evidence: $"Line {line.LineNumber}: {content}",
            whyItMatters: "Introducing synchronous overloads in an async codebase creates inconsistency and tempts callers to block with .Result, risking deadlocks in ASP.NET or UI contexts.",
            suggestedAction: $"Consider returning Task<T> and awaiting internally, or rename to make the synchronous intent explicit (e.g. '{methodName}Sync' or drop the action prefix).",
            confidence: Confidence.Low,
            line: line));
    }

    private void CheckStringComparisonAntiPattern(DiffLine line, DiffFile file, List<Finding> findings)
    {
        var content = line.Content;

        bool hasToLower = content.Contains(".ToLower()", StringComparison.Ordinal);
        bool hasToUpper = content.Contains(".ToUpper()", StringComparison.Ordinal);
        if (!hasToLower && !hasToUpper) return;

        bool inComparison =
            content.Contains(" == ", StringComparison.Ordinal) ||
            content.Contains(" != ", StringComparison.Ordinal) ||
            content.Contains(".Equals(", StringComparison.Ordinal) ||
            content.Contains(".Contains(", StringComparison.Ordinal) ||
            content.Contains(".StartsWith(", StringComparison.Ordinal) ||
            content.Contains(".EndsWith(", StringComparison.Ordinal);

        if (!inComparison) return;

        string which = hasToLower ? ".ToLower()" : ".ToUpper()";
        findings.Add(CreateFinding(file,
            summary: $"Culture-sensitive {which} used for string comparison in {file.NewPath}.",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: $"{which} allocates a new string and is culture-sensitive, producing incorrect results in Turkish and other locales. It is also inconsistent when the codebase uses StringComparison elsewhere.",
            suggestedAction: "Use string.Equals(a, b, StringComparison.OrdinalIgnoreCase) or .Contains(x, StringComparison.OrdinalIgnoreCase) instead of normalising case manually.",
            confidence: Confidence.Medium,
            line: line));
    }

    private static bool IsMethodDeclaration(string trimmed)
    {
        if (!trimmed.Contains('(')) return false;

        bool hasModifier =
            trimmed.StartsWith("public ", StringComparison.Ordinal) ||
            trimmed.StartsWith("private ", StringComparison.Ordinal) ||
            trimmed.StartsWith("protected ", StringComparison.Ordinal) ||
            trimmed.StartsWith("internal ", StringComparison.Ordinal) ||
            trimmed.StartsWith("static ", StringComparison.Ordinal) ||
            trimmed.StartsWith("override ", StringComparison.Ordinal) ||
            trimmed.StartsWith("virtual ", StringComparison.Ordinal) ||
            trimmed.StartsWith("abstract ", StringComparison.Ordinal) ||
            trimmed.StartsWith("sealed ", StringComparison.Ordinal);

        if (!hasModifier) return false;

        // Exclude attribute-decorated lines: "[Foo] public ..."
        int parenIdx = trimmed.IndexOf('(');
        string beforeParen = trimmed[..parenIdx];

        // Assignment before paren → invocation, not declaration
        if (beforeParen.Contains('=')) return false;

        // Must have at least one space-delimited word before the paren (the method name)
        var words = beforeParen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2;
    }

    private static string ExtractMethodName(string trimmed)
    {
        int parenIdx = trimmed.IndexOf('(');
        if (parenIdx < 0) return string.Empty;
        var words = trimmed[..parenIdx].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? words[^1] : string.Empty;
    }
}
