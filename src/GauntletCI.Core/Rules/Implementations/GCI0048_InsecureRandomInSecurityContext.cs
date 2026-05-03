// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0048, Insecure Random in Security Context
/// Detects <c>System.Random</c> instantiation within 5 lines of security-sensitive identifiers
/// such as <c>apikey</c>, <c>token</c>, <c>secret</c>, <c>password</c>, <c>privatekey</c>,
/// <c>accesskey</c>, <c>salt</c>, or similar compound security terms in non-test files.
/// <c>System.Random</c> is not cryptographically secure and must never be used to generate
/// tokens, keys, salts, passwords, or similar values.
/// </summary>
public class GCI0048_InsecureRandomInSecurityContext : RuleBase
{
    public GCI0048_InsecureRandomInSecurityContext(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id   => "GCI0048";
    public override string Name => "Insecure Random in Security Context";

    private static readonly Regex NewRandomRegex = new(
        @"\bnew\s+Random\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SecurityTerms =
    [
        "token", "secret", "password", "apikey", "api_key", "privatekey", "private_key",
        "accesskey", "access_key", "salt", "nonce", "credential", "passphrase", "hmac",
    ];

    private static bool IsAfterLineComment(string content, int matchIndex)
    {
        int commentStart = content.IndexOf("//", StringComparison.Ordinal);
        return commentStart >= 0 && commentStart < matchIndex;
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        var isNet8Plus = TargetFrameworkDetector.IsNet8OrLater(context.TargetFramework);
        var suggestedAction = isNet8Plus
            ? "Replace with RandomNumberGenerator.GetBytes() or RandomNumberGenerator.GetHexString() " +
              "(.NET 8+) for security-sensitive values."
            : "Replace with RandomNumberGenerator.GetBytes() (System.Security.Cryptography) " +
              "for security-sensitive values.";

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var line = addedLines[i];
                var match = NewRandomRegex.Match(line.Content);
                if (!match.Success) continue;

                // Syntax guard: suppress if the match position is inside a comment or string literal.
                if (context.Syntax?.IsInCommentOrStringLiteral(file.NewPath, line.LineNumber, match.Index) == true)
                    continue;

                // Lightweight fallback for raw-diff analysis (no syntax tree):
                // suppress when the match falls after a // comment marker on the same line.
                if (IsAfterLineComment(line.Content, match.Index)) continue;

                // Check ±5 surrounding added lines for security-sensitive identifiers
                int start = Math.Max(0, i - 5);
                int end   = Math.Min(addedLines.Count - 1, i + 5);

                bool nearSecurityTerm = false;
                for (int j = start; j <= end && !nearSecurityTerm; j++)
                {
                    var content = addedLines[j].Content.ToLowerInvariant();
                    foreach (var term in SecurityTerms)
                    {
                        if (content.Contains(term)) { nearSecurityTerm = true; break; }
                    }
                }

                if (!nearSecurityTerm) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "System.Random used near security-sensitive identifier: use a cryptographic RNG instead",
                    evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters: "System.Random is a pseudo-random number generator seeded from the system clock. " +
                                  "Its output is predictable and must never be used for cryptographic purposes " +
                                  "such as generating tokens, keys, salts, nonces, or passwords.",
                    suggestedAction: suggestedAction,
                    confidence: Confidence.High,
                    line: line));
            }
        }

        return Task.FromResult(findings);
    }
}

