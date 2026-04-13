// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0007 – Error Handling Integrity
/// Detects swallowed exceptions and empty catch blocks.
/// </summary>
public class GCI0007_ErrorHandlingIntegrity : RuleBase
{
    public override string Id => "GCI0007";
    public override string Name => "Error Handling Integrity";

    // Diverges intentionally from WellKnownPatterns.HighSeverityLogKeywords: this array matches
    // structured log method-call patterns (e.g. ".Error(", ".Fatal(") rather than bare keyword strings,
    // so it cannot be replaced by the shared keyword list without changing detection logic.
    private static readonly string[] HighSeverityLogPatterns =
        [".error(", ".Error(", "Errorf(", "ErrorS(", "level.Error(", "log.Error(",
         ".fatal(", ".Fatal(", ".Panic(", ".panic(", ".critical(", ".Critical("];

    private static readonly string[] ErrorHandlingKeywords =
        ["catch", "rescue", "if err", "except", "RecordError(", "span.SetStatus"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckSwallowedExceptions(diff, findings);
        CheckRemovedErrorContextLogging(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckSwallowedExceptions(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content.Trim();

                // Detect catch blocks
                if (!content.StartsWith("catch", StringComparison.Ordinal)) continue;

                // Cancellation exceptions are commonly swallowed intentionally (shutdown/background work).
                if (content.Contains("TaskCanceledException", StringComparison.Ordinal) ||
                    content.Contains("OperationCanceledException", StringComparison.Ordinal))
                {
                    continue;
                }

                bool isSwallowed = IsCatchSwallowed(addedLines, i, out string evidence);
                if (isSwallowed)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Swallowed exception detected in {file.NewPath}",
                        evidence: evidence,
                        whyItMatters: "Empty or silent catch blocks hide failures, making bugs invisible and debugging nearly impossible.",
                        suggestedAction: "Log the exception, rethrow it, or handle it explicitly. Never swallow silently.",
                        confidence: Confidence.High,
                        line: addedLines[i]));
                }
            }
        }
    }

    private static bool IsCatchSwallowed(List<DiffLine> addedLines, int catchIdx, out string evidence)
    {
        evidence = addedLines[catchIdx].Content.Trim();

        // Look for { and } around the catch body
        int depth = 0;
        bool inBody = false;
        bool hasContent = false;

        for (int j = catchIdx; j < Math.Min(addedLines.Count, catchIdx + 10); j++)
        {
            var line = addedLines[j].Content.Trim();
            foreach (char c in line)
            {
                if (c == '{') { depth++; inBody = true; }
                else if (c == '}') { depth--; }
            }

            if (inBody && j > catchIdx)
            {
                if (!string.IsNullOrWhiteSpace(line) && line != "{" && line != "}")
                {
                    // Check if it has throw, log, or meaningful content
                    bool hasThrow = line.Contains("throw", StringComparison.Ordinal);
                    bool hasLog = line.Contains("Log", StringComparison.Ordinal) ||
                                  line.Contains("log", StringComparison.Ordinal) ||
                                  line.Contains("Console.", StringComparison.Ordinal) ||
                                  line.Contains("Debug.", StringComparison.Ordinal) ||
                                  line.Contains("Trace.", StringComparison.Ordinal);
                    if (hasThrow || hasLog) return false;
                    hasContent = true;
                }
            }

            if (inBody && depth == 0) break;
        }

        // If the catch body had no meaningful content, it's swallowed
        return !hasContent;
    }

    private void CheckRemovedErrorContextLogging(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            int removedHighSev = file.RemovedLines
                .Count(l => HighSeverityLogPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

            if (removedHighSev == 0) continue;

            int addedHighSev = file.AddedLines
                .Count(l => HighSeverityLogPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

            if (addedHighSev >= removedHighSev) continue;

            bool hasErrorHandlingContext = file.Hunks.Any(hunk =>
                hunk.Lines.Any(l =>
                    (l.Kind == DiffLineKind.Context || l.Kind == DiffLineKind.Removed) &&
                    ErrorHandlingKeywords.Any(k => l.Content.Contains(k, StringComparison.OrdinalIgnoreCase))));

            if (!hasErrorHandlingContext) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Error-level logging removed from error handling block in {file.NewPath}.",
                evidence: $"{removedHighSev} error-level log call(s) removed, {addedHighSev} added in error-handling context.",
                whyItMatters: "Removing error logs from catch/rescue blocks leaves exceptions silent — critical failure context is lost for incident triage.",
                suggestedAction: "Preserve or replace the error logging so that failure context is not silently dropped.",
                confidence: Confidence.High));
        }
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA1031" or "CA2000" or "CA1001"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0007",
                RuleName = "Error Handling Integrity",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a potential resource or exception handling issue.",
                SuggestedAction = "Review and address the flagged exception/disposal issue.",
                Confidence = diag.Id == "CA1031" ? Confidence.High : Confidence.Medium,
            });
        }
    }
}
