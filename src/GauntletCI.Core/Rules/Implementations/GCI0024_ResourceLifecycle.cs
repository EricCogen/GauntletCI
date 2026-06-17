// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0024, Resource Lifecycle
/// Detects disposable resources allocated without a using statement or try/finally disposal.
/// Covers both explicit known types (FileStream, SqlConnection, …) and any type whose name
/// ends with a disposable suffix (Stream, Reader, Writer, Connection, Client, etc.).
/// Absorbs GCI0030 detection scope; GCI0030 is now superseded by this rule.
/// Boundary with GCI0039 (External Service Safety): GCI0039 owns new HttpClient() detection
/// (it enforces IHttpClientFactory usage). HttpClient is suppressed here to avoid double-reporting.
/// </summary>
public class GCI0024_ResourceLifecycle : RuleBase
{
    public GCI0024_ResourceLifecycle(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0024";
    public override string Name => "Resource Lifecycle";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckUnguardedDisposables(file, context, findings);
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckUnguardedDisposables(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) return;

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content;

            // Skip mock/fake resources in test code (even if test file guard was bypassed)
            if (WellKnownPatterns.HasMockPattern(content)) continue;

            var (typeName, isExplicit) = MatchDisposableType(content);
            if (typeName is null) continue;

            // Skip: options/default-only replacement of an existing new Type(...) allocation.
            if (IsReplacementOfExistingAllocation(allLines, i, typeName, content)) continue;

            if (context.Syntax is { } syntax &&
                !syntax.IsConfirmedObjectCreation(file.NewPath, line.LineNumber, typeName))
                continue;

            // Defer to the owning rule (GCI0039) rather than double-reporting.
            if (WellKnownPatterns.ResourcePatterns.OwnedByOtherRules.Contains(typeName)) continue;

            // Skip: `return new X(...)` or `return foo(new X(...))`: caller takes ownership.
            var trimmed = content.TrimStart();
            if (trimmed.StartsWith("return ", StringComparison.Ordinal)) continue;

            // Skip: field/property initializer (instance/DI-managed lifetime).
            if (IsFieldOrPropertyInitializer(content)) continue;

            // Skip: local assigned then passed to ctor/method (callee owns lifetime).
            if (IsAssignedThenPassedToCallee(allLines, i, content)) continue;

            // Skip: `new X(...)` inside a method/constructor call argument (incl. multi-line).
            if (IsInsideMethodCallArg(allLines, i, typeName)) continue;

            // Skip: `static readonly X = new X()`: process-lifetime singletons are never disposed
            // by design; flagging them produces only noise with no actionable fix.
            if (content.Contains("static ", StringComparison.Ordinal)) continue;

            if (content.Contains("using ", StringComparison.Ordinal)) continue;

            bool prevHasUsing = false;
            for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
            {
                var prev = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(prev)) continue;
                if (prev.StartsWith("using ") || prev.StartsWith("await using "))
                { prevHasUsing = true; break; }
                break;
            }
            if (prevHasUsing) continue;

            int winStart = Math.Max(0, i - 2);
            int winEnd = Math.Min(allLines.Count, i + 20);
            bool hasDispose = allLines[winStart..winEnd].Any(l =>
                l.Content.Contains(".Dispose()", StringComparison.Ordinal) ||
                l.Content.Contains("finally", StringComparison.Ordinal));
            if (hasDispose) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"{typeName} allocated without using statement in {file.NewPath}.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: $"{typeName} implements IDisposable. Without using, it leaks OS handles or connection pool slots under exceptions.",
                suggestedAction: $"Wrap in `using var resource = new {typeName}(...);` to guarantee disposal.",
                confidence: isExplicit ? Confidence.High : Confidence.Medium,
                line: line));
        }
    }

    private static (string? TypeName, bool IsExplicit) MatchDisposableType(string content)
    {
        // Fast path: explicit known types: High confidence
        foreach (var knownType in WellKnownPatterns.ResourcePatterns.DisposableTypes)
        {
            if (content.Contains(knownType, StringComparison.Ordinal))
                return (knownType.Replace("new ", "").TrimEnd('('), true);
        }

        // Suffix heuristic: Medium confidence
        var match = WellKnownPatterns.ResourcePatterns.NewTypeRegex.Match(content);
        if (match.Success)
        {
            var name = match.Groups[1].Value;
            foreach (var suffix in WellKnownPatterns.ResourcePatterns.DisposableSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    // Skip types known NOT to be disposable despite having a disposable-looking suffix
                    if (WellKnownPatterns.ResourcePatterns.KnownNonDisposableTypes.Contains(name)) return (null, false);
                    return (name, false);
                }
            }
        }

        return (null, false);
    }

    private static bool IsReplacementOfExistingAllocation(
        IReadOnlyList<DiffLine> allLines,
        int index,
        string typeName,
        string addedContent)
    {
        if (index <= 0) return false;

        for (int j = index - 1; j >= Math.Max(0, index - 3); j--)
        {
            var line = allLines[j];
            if (line.Kind != DiffLineKind.Removed) continue;

            if (!line.Content.Contains("new " + typeName, StringComparison.Ordinal)
                || !addedContent.Contains("new " + typeName, StringComparison.Ordinal))
            {
                continue;
            }

            // using/dispose removed in favor of unguarded allocation: keep the finding.
            if (line.Content.Contains("using ", StringComparison.Ordinal)
                || line.Content.Contains(".Dispose()", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsAssignedThenPassedToCallee(IReadOnlyList<DiffLine> allLines, int index, string content)
    {
        var assignMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"\bvar\s+(\w+)\s*=\s*new\s+");
        if (!assignMatch.Success) return false;

        var varName = assignMatch.Groups[1].Value;
        int winEnd = Math.Min(allLines.Count, index + 15);
        for (int j = index + 1; j < winEnd; j++)
        {
            var line = allLines[j];
            if (line.Kind == DiffLineKind.Removed) continue;

            var next = line.Content;
            if (next.Contains($"({varName}", StringComparison.Ordinal)
                || next.Contains($", {varName}", StringComparison.Ordinal)
                || next.Contains($"{varName},", StringComparison.Ordinal)
                || next.Contains($" {varName})", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFieldOrPropertyInitializer(string content)
    {
        if (!content.Contains(" = new ", StringComparison.Ordinal)) return false;

        var trimmed = content.TrimStart();
        return trimmed.StartsWith("private ", StringComparison.Ordinal)
            || trimmed.StartsWith("internal ", StringComparison.Ordinal)
            || trimmed.StartsWith("protected ", StringComparison.Ordinal)
            || trimmed.StartsWith("public ", StringComparison.Ordinal)
            || trimmed.Contains(" readonly ", StringComparison.Ordinal)
            || trimmed.Contains(" static ", StringComparison.Ordinal);
    }

    // Returns true when `new TypeName(` appears inside an open call/ctor argument list.
    // Looks back across preceding hunk lines (added or context) so multi-line args are covered.
    private static bool IsInsideMethodCallArg(IReadOnlyList<DiffLine> allLines, int index, string typeName)
    {
        const int maxLookback = 8;
        int opens = 0;
        int closes = 0;

        for (int j = index; j >= Math.Max(0, index - maxLookback); j--)
        {
            var line = allLines[j];
            if (line.Kind == DiffLineKind.Removed) continue;

            var content = line.Content;
            int end = content.Length;
            if (j == index)
            {
                var needle = "new " + typeName;
                int idx = content.IndexOf(needle, StringComparison.Ordinal);
                if (idx < 0) return opens > closes;
                end = idx;
            }

            for (int k = 0; k < end; k++)
            {
                if (content[k] == '(') opens++;
                else if (content[k] == ')') closes++;
            }
        }

        return opens > closes;
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA2000" or "CA1001" or "CA2213"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0024",
                RuleName = "Resource Lifecycle",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a resource that may not be properly disposed.",
                SuggestedAction = "Use a using statement or implement IDisposable correctly to ensure deterministic cleanup.",
                Confidence = Confidence.High,
            });
        }
    }
}

