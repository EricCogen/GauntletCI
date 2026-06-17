// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Shared regex-to-finding pipeline helpers: comment/string suppression, optional semantic
/// confirmation, and literal-site promotion for configuration-style rules.
/// </summary>
public static class RegexEvidencePromotion
{
    /// <summary>
    /// Validates a regex hit on executable code (e.g. method calls, object creation).
    /// Stage 3: suppress comment/string false positives. Stage 4: optional semantic confirm.
    /// </summary>
    public static bool PassesCodeCandidateValidation(
        AnalysisContext context,
        string filePath,
        DiffLine line,
        int matchIndex,
        Func<SyntaxContext, bool>? confirmSemantic = null,
        Func<string, bool>? allowWhenNoSyntaxTree = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(line);

        if (context.Syntax is { } syntax)
        {
            if (syntax.IsInCommentOrStringLiteral(filePath, line.LineNumber, matchIndex))
                return false;

            return confirmSemantic?.Invoke(syntax) ?? true;
        }

        if (allowWhenNoSyntaxTree is not null)
            return allowWhenNoSyntaxTree(line.Content);

        return !IsWholeLineComment(line.Content);
    }

    /// <summary>
    /// Validates that a candidate value appears in a Roslyn-confirmed string literal on the line.
    /// Pass-through when no syntax tree is available (uses <paramref name="extractLiteralsWhenNoTree"/>).
    /// </summary>
    public static bool PassesLiteralCandidateValidation(
        AnalysisContext context,
        string filePath,
        DiffLine line,
        Func<string, bool> literalPredicate,
        Func<string, IReadOnlyList<string>>? extractLiteralsWhenNoTree = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(literalPredicate);

        if (context.Syntax is { } syntax)
            return syntax.HasStringLiteralMatching(filePath, line.LineNumber, literalPredicate);

        if (extractLiteralsWhenNoTree is not null)
            return extractLiteralsWhenNoTree(line.Content).Any(literalPredicate);

        return true;
    }

    private static bool IsWholeLineComment(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal);
    }
}
