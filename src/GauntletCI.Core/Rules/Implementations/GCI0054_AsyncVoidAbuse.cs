// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0054, Async Void Abuse
/// Detects public async methods that return void instead of Task, which prevents callers
/// from awaiting and catching exceptions. Fire-and-forget async methods should only be
/// used in event handlers where Task return is impossible.
/// </summary>
public class GCI0054_AsyncVoidAbuse : RuleBase
{
    public override string Id => "GCI0054";
    public override string Name => "Async Void Abuse";

    private static readonly Regex AsyncVoidMethodRegex =
        new(@"(public|protected)\s+async\s+void\s+\w+\s*\(", RegexOptions.Compiled);

    private static readonly Regex EventHandlerRegex =
        new(@"(?:EventHandler|OnClick|OnChange|OnSubmit|_Clicked|_Changed|_Submitted)", RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        var diff = context.Diff;

        foreach (var file in diff.Files)
        {
            // Skip test files
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            CheckAsyncVoidMethods(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckAsyncVoidMethods(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            if (!AsyncVoidMethodRegex.IsMatch(line.Content)) continue;

            // Extract method name to check if it's an event handler
            var methodMatch = Regex.Match(line.Content, @"async\s+void\s+(\w+)\s*\(");
            if (!methodMatch.Success) continue;

            var methodName = methodMatch.Groups[1].Value;

            // Exception: Event handlers are permitted to use async void
            if (EventHandlerRegex.IsMatch(methodName) ||
                methodName.StartsWith("On", StringComparison.Ordinal) ||
                methodName.EndsWith("Handler", StringComparison.Ordinal))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Public method {methodName} is declared async void",
                evidence: $"{file.NewPath} line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Async void methods can't be awaited or have exceptions caught by callers. Any exception thrown will crash the synchronization context. This makes error handling impossible.",
                suggestedAction: "Return Task instead of void. Only use async void for event handlers where returning Task is impossible. For UI events, consider using a Task-returning method.",
                confidence: Confidence.High,
                line: line));
        }
    }
}
