// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Security boundaries, injection guards, credential detection, and security-focused analysis patterns.
/// Used by GCI0003, GCI0010, and GCI0012 for security analysis and change detection.
/// </summary>
internal static class SecurityPatterns
{
    /// <summary>
    /// Returns <c>true</c> if the value appears to be an environment variable name
    /// (ALL_CAPS with digits and underscores, e.g., GITHUB_TOKEN, MY_API_KEY).
    /// Used by GCI0012 to skip environment variable names from hardcoded credential detection.
    /// </summary>
    public static bool IsEnvVarName(string literal) =>
        literal.Length > 0 && literal.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

    /// <summary>
    /// Returns <c>true</c> for benign literal values that are never actual secrets:
    /// empty strings, short strings (&lt;3 chars), HTTP auth scheme names, and C# keyword literals.
    /// Used by GCI0012 to reduce false positives in credential detection.
    /// </summary>
    public static bool IsBenignLiteralValue(string value) =>
        string.IsNullOrEmpty(value) ||
        value.Length < 3 ||
        value.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Basic", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Token", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Anonymous", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the index of the first real assignment = in the line, skipping string literals.
    /// Distinguishes between = (assignment), == (equality), !=, &lt;=, >=, and => (lambda/expression body).
    /// Used by GCI0012 to find credentials assigned to variables.
    /// </summary>
    public static int FindAssignmentIndex(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        bool inString = false;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '"' && (i == 0 || content[i - 1] != '\\'))
                inString = !inString;
            if (inString || content[i] != '=') continue;
            char prev = i > 0 ? content[i - 1] : '\0';
            char next = i < content.Length - 1 ? content[i + 1] : '\0';
            if (prev is '!' or '<' or '>' or '=') continue;
            if (next is '=' or '>') continue;  // == and =>
            return i;
        }
        return content.Length;
    }

    /// <summary>
    /// Returns <c>true</c> only if the line contains a real assignment = (not ==, !=, <=, >=, =>).
    /// Skips = signs inside string literals to avoid false positives from format strings.
    /// Used by GCI0012 to detect variable assignments with hardcoded credentials.
    /// </summary>
    public static bool HasAssignment(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return FindAssignmentIndex(content) < content.Length;
    }

    /// <summary>
    /// Returns the string value only when the direct RHS of an assignment is a bare string literal.
    /// Returns null if the RHS is a method call, object initializer, or anything other than a literal.
    /// Prevents false positives from patterns like: _tokenField = SomeFactory("ui-element-id")
    /// Used by GCI0012 to find actual hardcoded credential values.
    /// </summary>
    public static string? ExtractDirectlyAssignedLiteral(string content)
    {
        int eqIdx = FindAssignmentIndex(content);
        if (eqIdx >= content.Length) return null;

        var rhs = content[(eqIdx + 1)..].TrimStart();

        // Must open with a string literal: not a method call, `new`, identifier, etc.
        if (!rhs.StartsWith('"') &&
            !rhs.StartsWith("@\"", StringComparison.Ordinal) &&
            !rhs.StartsWith("$\"", StringComparison.Ordinal))
            return null;

        try
        {
            var wrapped = $"class __G {{ void __M() {{ var __v = {rhs.TrimEnd(';', ' ', ',')}; }} }}";
            var tree = CSharpSyntaxTree.ParseText(wrapped);
            return tree.GetRoot()
                .DescendantTokens()
                .Where(t => t.Kind() == SyntaxKind.StringLiteralToken)
                .Select(t => t.ValueText)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Commit message keywords indicating security-focused changes.
    /// Used by GCI0003 for behavioral change context analysis.
    /// </summary>
    public static readonly string[] SecurityKeywords =
    [
        "CVE", "security", "vulnerability", "fix", "DoS", "infinite",
        "loop", "exhaustion", "exception", "error", "RFC", "compliance",
        "boundary", "validation", "attack", "malicious", "payload", "regression"
    ];

    /// <summary>
    /// Test pattern keywords indicating security-focused test additions.
    /// Used by GCI0003 for detecting security-focused test additions.
    /// </summary>
    public static readonly string[] SecurityTestPatterns =
    [
        "Error", "Exception", "Timeout", "Exhaustion", "Attack",
        "Craft", "Malicious", "Payload", "CVE", "Boundary", "Validation"
    ];

    /// <summary>
    /// Returns <c>true</c> if the given text contains security-related keywords.
    /// Used by GCI0003 for analyzing commit messages for security focus.
    /// </summary>
    public static bool HasSecurityKeywords(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var lowerText = text.ToLowerInvariant();
        return SecurityKeywords.Any(k =>
            lowerText.Contains(k.ToLowerInvariant(), StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> if the given text contains security-related test patterns.
    /// Used by GCI0003 for detecting security-focused test additions.
    /// </summary>
    public static bool HasSecurityTestPattern(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return SecurityTestPatterns.Any(p =>
            text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}
