// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

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
        "dateofbirth", "passport", "nationalid", "taxid", "bankaccount",
        "name", "address", "dob", "birthdate", "username", "zipcode", "postalcode",
        "geolocation", "ipaddress", "deviceid", "token"
    ];

    private static readonly string[] LogPrefixes =
    [
        "_logger.", "logger.", "Logger.", "_log.", "log.", "Log.Information", "Log.Warning",
        "Log.Error", "Log.Debug", "Log.Critical", "Log.Write"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
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
                    if (ContainsPiiTerm(content, term))
                    { matchedTerm = term; break; }
                }
                if (matchedTerm is null) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"PII term '{matchedTerm}' found in log call — may expose sensitive data in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Logging PII violates GDPR, CCPA, and HIPAA. Once in logs, PII propagates to log aggregators, storage, and third-party monitoring tools.",
                    suggestedAction: "Redact or omit PII from log calls. Log only anonymized identifiers (e.g. UserId, not Email or SSN).",
                    confidence: Confidence.High,
                    line: line));
            }
        }

        return Task.FromResult(findings);
    }

    private static bool ContainsPiiTerm(string content, string term)
    {
        int idx = 0;
        while (idx < content.Length)
        {
            int found = content.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;

            bool prevOk = found == 0 || !IsWordChar(content[found - 1]);
            bool nextOk = found + term.Length >= content.Length || !IsWordChar(content[found + term.Length]);

            if (prevOk && nextOk) return true;
            idx = found + 1;
        }
        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
