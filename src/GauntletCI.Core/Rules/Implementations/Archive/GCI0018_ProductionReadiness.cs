// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0018 – Production Readiness
/// Checks for TODO/FIXME markers, NotImplementedException, and debug artifacts.
/// The "aggregate >3 other rules" synthesis is handled by RuleOrchestrator.PostProcess().
/// </summary>
[ArchivedRule("Vague meta-checklist; too broad to produce actionable findings")]
public class GCI0018_ProductionReadiness : RuleBase
{
    public override string Id => "GCI0018";
    public override string Name => "Production Readiness";

    private static readonly string[] MarkerKeywords = ["TODO", "FIXME", "HACK", "XXX"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckTodoMarkers(diff, findings);
        CheckNotImplemented(diff, findings);
        CheckDebugArtifacts(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckTodoMarkers(DiffContext diff, List<Finding> findings)
    {
        var markerLines = diff.AllAddedLines
            .Where(l => MarkerKeywords.Any(k => l.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (markerLines.Count == 0) return;

        findings.Add(CreateFinding(
            summary: $"{markerLines.Count} TODO/FIXME/HACK marker(s) in added code.",
            evidence: string.Join(" | ", markerLines.Take(5).Select(l => $"L{l.LineNumber}: {l.Content.Trim()}")),
            whyItMatters: "TODO markers indicate unfinished work being shipped to production.",
            suggestedAction: "Resolve all TODO/FIXME items before merging, or convert to tracked issues.",
            confidence: Confidence.Medium));
    }

    private void CheckNotImplemented(DiffContext diff, List<Finding> findings)
    {
        var niLines = diff.AllAddedLines
            .Where(l => l.Content.Contains("throw new NotImplementedException(", StringComparison.Ordinal))
            .ToList();

        if (niLines.Count == 0) return;

        findings.Add(CreateFinding(
            summary: $"{niLines.Count} NotImplementedException throw(s) in added code.",
            evidence: string.Join(", ", niLines.Take(3).Select(l => $"L{l.LineNumber}")),
            whyItMatters: "NotImplementedException in production code will crash callers at runtime.",
            suggestedAction: "Implement the missing logic before merging.",
            confidence: Confidence.Medium));
    }

    private void CheckDebugArtifacts(DiffContext diff, List<Finding> findings)
    {
        // Console.WriteLine in non-test, non-CLI files
        foreach (var file in diff.Files)
        {
            // Diverges intentionally from WellKnownPatterns.IsTestFile: inline check scoped to this
            // method's context; only "Test"/"Spec" name fragments are relevant here.
            bool isTestFile = file.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                              file.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase);
            bool isCliFile = file.NewPath.Contains("Cli", StringComparison.OrdinalIgnoreCase) ||
                             file.NewPath.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) ||
                             file.NewPath.Contains("ConsoleReporter", StringComparison.OrdinalIgnoreCase);

            if (isTestFile || isCliFile) continue;

            var consoleLines = file.AddedLines
                .Where(l => l.Content.Contains("Console.WriteLine(", StringComparison.Ordinal) ||
                             l.Content.Contains("Console.Write(", StringComparison.Ordinal))
                .ToList();

            if (consoleLines.Count > 0)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Console.WriteLine() in production code: {file.NewPath}",
                    evidence: string.Join(", ", consoleLines.Take(3).Select(l => $"L{l.LineNumber}")),
                    whyItMatters: "Console output bypasses the logging infrastructure and is lost in production environments.",
                    suggestedAction: "Replace Console.Write* with structured logging via ILogger.",
                    confidence: Confidence.Medium));
            }

            var debugAsserts = file.AddedLines
                .Where(l => l.Content.Contains("Debug.Assert(", StringComparison.Ordinal))
                .ToList();

            if (debugAsserts.Count > 0)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Debug.Assert() in production code: {file.NewPath}",
                    evidence: string.Join(", ", debugAsserts.Take(3).Select(l => $"L{l.LineNumber}")),
                    whyItMatters: "Debug.Assert() is stripped in Release builds and should not be used for runtime validation.",
                    suggestedAction: "Use proper exceptions or conditional checks that work in Release configuration.",
                    confidence: Confidence.Medium));
            }
        }
    }
}
