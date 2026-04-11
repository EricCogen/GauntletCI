// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0029 – PII Entity Logging Leak
/// Detects PII terms in log calls in added lines of .cs files.
/// See also: GCI0023 (Structured Logging) — detects format issues in log calls.
/// These rules are complementary: GCI0029 checks content (PII), GCI0023 checks format.
/// </summary>
public class GCI0029_PiiLoggingLeak : RuleBase
{
    public override string Id => "GCI0029";
    public override string Name => "PII Entity Logging Leak";

    private static readonly string[] PiiTerms =
    [
        "email", "ssn", "socialsecurity", "phonenumber", "creditcard", "cardnumber",
        "dateofbirth", "passport", "nationalid", "taxid", "bankaccount"
    ];

    private static readonly string[] LogPrefixes =
    [
        "_logger.", "logger.", "Logger.", "_log.", "log.", "Log.Information", "Log.Warning",
        "Log.Error", "Log.Debug", "Log.Critical", "Log.Write"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var line in file.AddedLines)
            {
                var content = line.Content;

                bool hasLogPrefix = false;
                foreach (var prefix in LogPrefixes)
                {
                    if (content.Contains(prefix, StringComparison.Ordinal))
                    { hasLogPrefix = true; break; }
                }
                if (!hasLogPrefix) continue;

                string? matchedTerm = null;
                foreach (var term in PiiTerms)
                {
                    if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
                    { matchedTerm = term; break; }
                }
                if (matchedTerm is null) continue;

                findings.Add(CreateFinding(
                    summary: $"PII term '{matchedTerm}' found in log call — may expose sensitive data in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Logging PII violates GDPR, CCPA, and HIPAA. Once in logs, PII propagates to log aggregators, storage, and third-party monitoring tools.",
                    suggestedAction: "Redact or omit PII from log calls. Log only anonymized identifiers (e.g. UserId, not Email or SSN).",
                    confidence: Confidence.High));
            }
        }

        return Task.FromResult(findings);
    }
}
