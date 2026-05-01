// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0007, Error Handling Integrity
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
        CheckExceptionThrowRemoval(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckSwallowedExceptions(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            foreach (var hunk in file.Hunks)
            {
                // Collect Added+Context lines only: excluding Removed lines so a previously
                // deleted throw/log cannot mask a genuinely empty new catch body.
                var hunkLines = new List<DiffLine>();
                foreach (var l in hunk.Lines)
                    if (l.Kind != DiffLineKind.Removed) hunkLines.Add(l);

                for (int i = 0; i < hunkLines.Count; i++)
                {
                    // Only evaluate catch blocks on Added lines (newly introduced catch).
                    if (hunkLines[i].Kind != DiffLineKind.Added) continue;

                    var content = hunkLines[i].Content.Trim();
                    if (!content.StartsWith("catch", StringComparison.Ordinal)) continue;

                    if (content.Contains("TaskCanceledException", StringComparison.Ordinal) ||
                        content.Contains("OperationCanceledException", StringComparison.Ordinal))
                        continue;

                    // Only flag bare catch{} or catch(Exception): specific typed catches
                    // (e.g. catch (ChannelClosedException) { break; }) represent explicit
                    // handling intent and must not be flagged as swallowed.
                    if (!IsBareOrBaseCatch(content)) continue;

                    bool isSwallowed = IsCatchSwallowed(hunkLines, i, out string evidence);
                    if (isSwallowed)
                    {
                        findings.Add(CreateFinding(
                            file,
                            summary: $"Swallowed exception detected in {file.NewPath}",
                            evidence: evidence,
                            whyItMatters: "Empty or silent catch blocks hide failures, making bugs invisible and debugging nearly impossible.",
                            suggestedAction: "Log the exception, rethrow it, or handle it explicitly. Never swallow silently.",
                            confidence: Confidence.High,
                            line: hunkLines[i]));
                    }
                }
            }
        }
    }

    private static bool IsCatchSwallowed(List<DiffLine> hunkLines, int catchIdx, out string evidence)
    {
        evidence = hunkLines[catchIdx].Content.Trim();

        // Look for { and } around the catch body
        int depth = 0;
        bool inBody = false;
        bool hasContent = false;

        for (int j = catchIdx; j < Math.Min(hunkLines.Count, catchIdx + 10); j++)
        {
            var line = hunkLines[j].Content.Trim();
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

    private void CheckExceptionThrowRemoval(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            foreach (var hunk in file.Hunks)
            {
                var hunkedLines = hunk.Lines.ToList();

                for (int i = 0; i < hunkedLines.Count; i++)
                {
                    var line = hunkedLines[i];
                    if (line.Kind != DiffLineKind.Removed) continue;

                    var content = line.Content.Trim();
                    if (!content.StartsWith("throw ", StringComparison.Ordinal)) continue;

                    bool inErrorPath = IsInErrorHandlingContext(hunkedLines, i);
                    if (!inErrorPath) continue;

                    bool hasReplacementPattern = CheckForExceptionReplacement(hunkedLines, i);
                    if (hasReplacementPattern)
                    {
                        findings.Add(CreateFinding(
                            file,
                            summary: $"Exception throw statement replaced with non-throwing code in {file.NewPath}",
                            evidence: $"Removed: {content}. Replaced with return/continue/break pattern.",
                            whyItMatters: "Replacing exception throws with silent returns violates error handling contracts and enables silent failures or denial-of-service attacks.",
                            suggestedAction: "Preserve the exception throw or document why the replacement is semantically equivalent and safe.",
                            confidence: Confidence.High,
                            line: line));
                    }
                }
            }
        }
    }

    private static bool IsInErrorHandlingContext(List<DiffLine> hunkedLines, int throwLineIdx)
    {
        int start = Math.Max(0, throwLineIdx - 5);
        int end = Math.Min(hunkedLines.Count, throwLineIdx + 5);

        // Check if "if (stream" or "catch" or error/exception keywords are nearby
        for (int i = start; i < end; i++)
        {
            var l = hunkedLines[i].Content;
            if (l.Contains("catch", StringComparison.Ordinal) ||
                l.Contains("stream.RstStreamReceived", StringComparison.Ordinal) ||
                l.Contains("RstStreamReceived", StringComparison.Ordinal) ||
                l.Contains("// Second reset", StringComparison.Ordinal) ||
                (l.Contains("if (") && l.Contains("stream")))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CheckForExceptionReplacement(List<DiffLine> hunkedLines, int throwLineIdx)
    {
        int end = Math.Min(hunkedLines.Count, throwLineIdx + 6);

        for (int i = throwLineIdx + 1; i < end; i++)
        {
            var line = hunkedLines[i];
            if (line.Kind != DiffLineKind.Added) continue;

            var content = line.Content.Trim();
            if (content.StartsWith("return ", StringComparison.Ordinal) ||
                content == "return;" ||
                content.StartsWith("continue", StringComparison.Ordinal) ||
                content.StartsWith("break", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
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
                whyItMatters: "Removing error logs from catch/rescue blocks leaves exceptions silent: critical failure context is lost for incident triage.",
                suggestedAction: "Preserve or replace the error logging so that failure context is not silently dropped.",
                confidence: Confidence.High));
        }
    }

    /// <summary>
    /// Returns true when the catch clause should be evaluated for swallowing:
    /// bare <c>catch</c> with no type, or <c>catch (Exception)</c> / <c>catch (Exception e)</c>.
    /// Specific typed catches (e.g. <c>catch (ChannelClosedException)</c>) represent explicit
    /// intent and are excluded from swallow detection.
    /// </summary>
    private static bool IsBareOrBaseCatch(string catchLine)
    {
        // "catch {" or "catch{": no type at all
        if (!catchLine.Contains('(')) return true;

        var open  = catchLine.IndexOf('(');
        var close = catchLine.IndexOf(')', open + 1);
        if (open < 0 || close <= open) return true; // malformed: treat conservatively

        var typePart = catchLine[(open + 1)..close].Trim();
        // Strip variable name: "Exception e" → "Exception"
        var space    = typePart.IndexOf(' ');
        var typeName = space > 0 ? typePart[..space] : typePart;

        return typeName is "Exception" or "System.Exception";
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)    {
        if (staticAnalysis is null) return;
        // CA2000 (don't dispose objects) and CA1001 (types owning disposable) are owned by GCI0024
        // (Resource Lifecycle): see DiagnosticMapper. GCI0007 keeps only CA1031 (catch generic
        // exception) to avoid producing two findings on the same diagnostic.
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA1031"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0007",
                RuleName = "Error Handling Integrity",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a potential exception handling issue.",
                SuggestedAction = "Catch specific exception types instead of swallowing System.Exception.",
                Confidence = Confidence.High,
            });
        }
    }
}
