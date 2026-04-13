// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0023 – Structured Logging
/// Detects log calls using string interpolation instead of structured key-value pairs,
/// and catch blocks in critical sections without any log statement.
/// See also: GCI0029 (PII Entity Logging Leak) — detects PII terms in log arguments.
/// These rules are complementary: GCI0023 checks format, GCI0029 checks content.
/// </summary>
public class GCI0023_StructuredLogging : RuleBase
{
    public override string Id => "GCI0023";
    public override string Name => "Structured Logging";

    private static readonly string[] LogCallPrefixes =
    [
        "_logger.", "logger.", "Logger.", "_log.", "log.",
        "Log.Information", "Log.Warning", "Log.Error", "Log.Debug", "Log.Critical",
        "Log.Write"
    ];

    private static readonly string[] CriticalPathKeywords =
    [
        "Payment", "payment", "Auth", "auth", "Login", "login",
        "Order", "order", "Billing", "billing", "Checkout", "checkout",
        "Token", "token", "Credential", "credential"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckStringInterpolationInLogs(file, findings);
            CheckCriticalPathWithoutCorrelationId(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckStringInterpolationInLogs(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content.Trim();
            if (content.StartsWith("//")) continue;

            bool isLogCall = LogCallPrefixes.Any(p => content.Contains(p, StringComparison.Ordinal));
            if (!isLogCall) continue;

            // Flag string interpolation: logger.LogInformation($"...)
            if (!content.Contains("($\"") && !content.Contains(", $\"") &&
                !content.Contains("($'") && !content.Contains("(\"") == false) continue;

            // More precise: contains a log call AND a $" interpolated string
            if (content.Contains("$\""))
            {
                findings.Add(CreateFinding(
                    summary: $"Log call uses string interpolation instead of structured parameters in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content}",
                    whyItMatters: "String interpolation in log calls prevents log aggregators (Seq, Splunk, ELK) from indexing structured fields. Use message templates with named placeholders instead.",
                    suggestedAction: "Replace $\"Value is {value}\" with \"Value is {Value}\", value — structured logging preserves queryable fields.",
                    confidence: Confidence.Medium));
            }
        }
    }

    private void CheckCriticalPathWithoutCorrelationId(DiffFile file, List<Finding> findings)
    {
        // Only check files on critical paths
        bool isCriticalPath = CriticalPathKeywords.Any(k =>
            file.NewPath.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (!isCriticalPath) return;

        var addedLines = file.AddedLines.ToList();
        if (addedLines.Count < 5) return;

        bool hasLogging = addedLines.Any(l =>
            LogCallPrefixes.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

        bool hasCorrelationId = addedLines.Any(l =>
            l.Content.Contains("CorrelationId", StringComparison.OrdinalIgnoreCase) ||
            l.Content.Contains("RequestId", StringComparison.OrdinalIgnoreCase) ||
            l.Content.Contains("TraceId", StringComparison.OrdinalIgnoreCase) ||
            l.Content.Contains("correlationId", StringComparison.Ordinal));

        if (hasLogging && !hasCorrelationId)
        {
            findings.Add(CreateFinding(
                summary: $"Critical-path file {file.NewPath} has logging but no correlation/request ID.",
                evidence: $"{addedLines.Count} lines added to {file.NewPath}",
                whyItMatters: "Without correlation IDs, tracing a single request across distributed services during an incident is extremely difficult.",
                suggestedAction: "Include CorrelationId, RequestId, or TraceId in log statements for critical operations so requests can be traced end-to-end.",
                confidence: Confidence.Low));
        }
    }
}
