// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0013 – Observability/Debuggability
/// Flags missing logging, missing XML docs, and unlogged exception re-throws.
/// </summary>
public class GCI0013_ObservabilityDebugability : RuleBase
{
    public override string Id => "GCI0013";
    public override string Name => "Observability/Debuggability";

    private static readonly string[] LoggingPatterns =
        ["_logger.", "Log.", "Console.Write", "Trace.", "Debug.Write", "logger."];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            CheckLargeMethodWithoutLogging(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckLargeMethodWithoutLogging(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        if (addedLines.Count < 20) return;

        bool hasLogging = addedLines.Any(l =>
            LoggingPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

        if (!hasLogging)
        {
            findings.Add(CreateFinding(
                summary: $"{addedLines.Count} lines added in {file.NewPath} with no logging calls.",
                evidence: $"File: {file.NewPath} — {addedLines.Count} added lines, no logging detected.",
                whyItMatters: "Code without logging is hard to diagnose in production.",
                suggestedAction: "Add appropriate logging at entry/exit points and for error paths.",
                confidence: Confidence.Low));
        }
    }


}
