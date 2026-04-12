// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0009 – Consistency with Patterns
/// Detects deviations from project-wide async/await and guard clause patterns.
/// </summary>
public class GCI0009_ConsistencyWithPatterns : RuleBase
{
    public override string Id => "GCI0009";
    public override string Name => "Consistency with Patterns";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckAsyncPattern(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckAsyncPattern(DiffContext diff, List<Finding> findings)
    {
        // Check if the existing diff context has async patterns
        var allLines = diff.Files.SelectMany(f => f.Hunks.SelectMany(h => h.Lines)).ToList();

        bool projectUsesAsync = allLines
            .Where(l => l.Kind == DiffLineKind.Context)
            .Any(l => l.Content.Contains("async Task", StringComparison.Ordinal));

        if (!projectUsesAsync) return;

        // Look for new non-async methods that could be async
        var newNonAsyncMethods = diff.AllAddedLines
            .Where(l =>
            {
                var t = l.Content.Trim();
                return (t.StartsWith("public ", StringComparison.Ordinal) ||
                        t.StartsWith("private ", StringComparison.Ordinal) ||
                        t.StartsWith("protected ", StringComparison.Ordinal)) &&
                       t.Contains('(') &&
                       !t.Contains("async ", StringComparison.Ordinal) &&
                       !t.Contains("void ", StringComparison.Ordinal) &&
                       (t.Contains(" Task") || t.Contains("Async("));
            })
            .ToList();

        if (newNonAsyncMethods.Count > 0)
        {
            findings.Add(CreateFinding(
                summary: "New methods appear to return Task but are not marked async.",
                evidence: string.Join(", ", newNonAsyncMethods.Take(3).Select(l => l.Content.Trim())),
                whyItMatters: "Inconsistent async patterns make code confusing and can lead to deadlocks.",
                suggestedAction: "Use async/await consistently or return Task.FromResult() for synchronous completions.",
                confidence: Confidence.Low));
        }
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA1305" or "CA1307" or "CA1309" or "CA1711" or "CA1720"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0009",
                RuleName = "Consistency with Patterns",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Inconsistent string comparison or naming conventions reduce code clarity.",
                SuggestedAction = "Follow established naming and string-comparison conventions.",
                Confidence = Confidence.Low,
            });
        }
    }
}
