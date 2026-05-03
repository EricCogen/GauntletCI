// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for security-related heuristics:
/// GCI0012 - Hardcoded secrets, weak cryptography, SQL injection vulnerabilities.
/// </summary>
public sealed class SecurityPatternStrategy : IInferenceStrategy
{
    private static readonly Regex SqlStringLiteralStart = new(@"\bSELECT\b|\bINSERT\b|\bUPDATE\b|\bDELETE\b|\bFROM\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly string[] CredentialKeywords =
    [
        "password", "passwd", "secret", "apikey", "api_key", "token",
        "credential", "private_key", "privatekey", "access_key", "auth_key"
    ];

    /// <summary>
    /// Applies GCI0012 heuristics: credential exposure, weak hashing, SQL injection.
    /// Only analyzes production .cs lines (not test files) to avoid false positives.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        // GCI0012 only applies to production code (not test files)
        if (context.ProductionAddedLines.Count == 0)
            return labels;

        bool hasCredential = context.ProductionAddedLines.Any(IsCredentialAssignment);
        bool hasWeakHash = context.ProductionAddedLines.Any(IsWeakHashUsage);
        bool hasSqlInjection = context.ProductionAddedLines.Any(IsSqlInjectionVulnerability);

        if (hasCredential || hasWeakHash || hasSqlInjection)
        {
            var reason = hasWeakHash ? "Diff adds use of weak hashing algorithm (MD5 or SHA1) in production code"
                       : hasSqlInjection ? "Diff builds SQL string via concatenation or interpolation in production code"
                       : "Diff contains credential keyword assigned to a literal string value on added production lines";
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0012",
                ShouldTrigger = true,
                ExpectedConfidence = 0.70,
                Reason = reason,
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }

    /// <summary>
    /// Returns true if the line is a credential assignment to a literal string.
    /// Checks for keywords like "password", "apikey", "token" assigned to string literals.
    /// </summary>
    private static bool IsCredentialAssignment(string line)
    {
        if (line.TrimStart().StartsWith("//"))
            return false;

        var lower = line.ToLowerInvariant();
        
        // Must contain a credential keyword
        if (!CredentialKeywords.Any(keyword => lower.Contains(keyword)))
            return false;

        // Must have an assignment operator
        if (!line.Contains("="))
            return false;

        // Must assign to a string literal (not a variable or method call)
        var eqIdx = line.IndexOf('=');
        if (eqIdx < 0 || eqIdx == line.Length - 1)
            return false;

        var afterEq = line[(eqIdx + 1)..].TrimStart();
        
        // Check for string literal assignment
        return afterEq.StartsWith("\"") || 
               afterEq.StartsWith("@\"") || 
               afterEq.StartsWith("$\"");
    }

    /// <summary>
    /// Returns true if the line uses weak hashing algorithms (MD5, SHA1).
    /// </summary>
    private static bool IsWeakHashUsage(string line)
    {
        if (line.TrimStart().StartsWith("//"))
            return false;

        return line.Contains("MD5.Create()", StringComparison.Ordinal) ||
               line.Contains("SHA1.Create()", StringComparison.Ordinal) ||
               line.Contains("new MD5CryptoServiceProvider(", StringComparison.Ordinal) ||
               line.Contains("new SHA1Managed(", StringComparison.Ordinal) ||
               line.Contains("new SHA1CryptoServiceProvider(", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the line has SQL injection vulnerability:
    /// SQL string literal built via string concatenation or interpolation.
    /// </summary>
    private static bool IsSqlInjectionVulnerability(string line)
    {
        if (line.TrimStart().StartsWith("//"))
            return false;

        // Must be a SQL string literal
        if (!SqlStringLiteralStart.IsMatch(line))
            return false;

        // Must use unsafe concatenation or interpolation
        return line.Contains(" + ", StringComparison.Ordinal) ||
               (line.Contains("{", StringComparison.Ordinal) && line.Contains("$\"", StringComparison.Ordinal)) ||
               line.Contains("string.Format(", StringComparison.Ordinal);
    }
}
