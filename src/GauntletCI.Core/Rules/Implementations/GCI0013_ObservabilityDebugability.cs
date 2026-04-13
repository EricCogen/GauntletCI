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

    private static readonly string[] HighSeverityLogPatterns =
        [".error(", ".Error(", "Errorf(", "ErrorS(", "level.Error(", "log.Error(",
         ".fatal(", ".Fatal(", ".Panic(", ".panic(", ".critical(", ".Critical("];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckRemovedErrorLogging(diff, findings);

        foreach (var file in diff.Files)
        {
            CheckLargeMethodWithoutLogging(file, findings);
            CheckPublicApiWithoutXmlDocs(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckRemovedErrorLogging(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            int removedHighSev = file.RemovedLines
                .Count(l => HighSeverityLogPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

            if (removedHighSev == 0) continue;

            int addedHighSev = file.AddedLines
                .Count(l => HighSeverityLogPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

            if (addedHighSev >= removedHighSev) continue;

            findings.Add(CreateFinding(
                summary: $"High-severity error logging removed in {file.NewPath}.",
                evidence: $"{removedHighSev} error-level log call(s) removed, {addedHighSev} added.",
                whyItMatters: "Removing error-level logs on failure paths silences critical runtime diagnostics needed for production incident triage.",
                suggestedAction: "Preserve error logging on failure paths, or ensure equivalent logging exists at the call site.",
                confidence: Confidence.High));
        }
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

    private void CheckPublicApiWithoutXmlDocs(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        for (int i = 0; i < addedLines.Count; i++)
        {
            var content = addedLines[i].Content.Trim();
            if (!IsPublicMethodOrClass(content)) continue;

            bool hasPrecedingXmlDoc = i > 0 &&
                addedLines[i - 1].Content.Trim().StartsWith("///", StringComparison.Ordinal);

            if (!hasPrecedingXmlDoc)
            {
                findings.Add(CreateFinding(
                    summary: $"Public API member without XML documentation in {file.NewPath}.",
                    evidence: $"Line {addedLines[i].LineNumber}: {content}",
                    whyItMatters: "Missing XML docs reduce IntelliSense quality and make the API harder to use correctly.",
                    suggestedAction: "Add /// <summary>...</summary> XML documentation to all public members.",
                    confidence: Confidence.Low));
                break; // one finding per file to reduce noise
            }
        }
    }

    private static bool IsPublicMethodOrClass(string line) =>
        line.StartsWith("public ", StringComparison.Ordinal) &&
        (line.Contains('(') || line.Contains(" class ") || line.Contains(" interface ") ||
         line.Contains(" record ") || line.Contains(" struct "));
}
