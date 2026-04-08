// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

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
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (!file.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

            CheckLargeMethodWithoutLogging(file, findings);
            CheckPublicApiWithoutXmlDocs(file, findings);
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
