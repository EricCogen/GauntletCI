// SPDX-License-Identifier: Elastic-2.0
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Provides syntax-tree-based guards for reducing false positives in regex-based rules.
/// All methods operate on a pre-parsed <see cref="SyntaxTree"/> and are intentionally
/// lightweight — no semantic model, no compilation, no disk I/O.
/// </summary>
public static class SyntaxGuard
{
    /// <summary>
    /// Returns <c>true</c> when the given 1-based <paramref name="lineNumber"/> contains
    /// an <c>ObjectCreationExpression</c> (i.e. <c>new T(...)</c>) whose simple type name
    /// matches <paramref name="typeName"/>. Useful to confirm a regex hit like
    /// <c>new Random(</c> is an actual object creation, not text inside a comment or string.
    /// </summary>
    public static bool HasObjectCreation(SyntaxTree tree, int lineNumber, string typeName)
    {
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var lineSpan = text.Lines[lineNumber - 1].Span;
        return tree.GetRoot()
            .DescendantNodes(lineSpan)
            .OfType<ObjectCreationExpressionSyntax>()
            .Any(n => GetSimpleTypeName(n.Type).Equals(typeName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> when the given 1-based <paramref name="lineNumber"/> falls
    /// inside comment trivia or a string/interpolated-string literal token.
    /// Use to suppress rules when the triggering text is quoted or commented-out code
    /// rather than live executable code — including end-of-line comments that the
    /// simple <c>StartsWith("//")</c> check misses.
    /// </summary>
    public static bool IsInCommentOrStringLiteral(SyntaxTree tree, int lineNumber)
    {
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var line = text.Lines[lineNumber - 1];
        var root = tree.GetRoot();

        // Step 1: check all tokens whose Span overlaps with this line.
        // Finds string literals anywhere on the line, and trailing-trivia comments.
        foreach (var token in root.DescendantTokens(line.Span))
        {
            // String literal tokens
            if (token.IsKind(SyntaxKind.StringLiteralToken)          ||
                token.IsKind(SyntaxKind.InterpolatedStringTextToken) ||
                token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken))
                return true;

            // Walk up ancestors to catch content inside string expression nodes
            var node = token.Parent;
            while (node is not null)
            {
                if (node.IsKind(SyntaxKind.StringLiteralExpression)   ||
                    node.IsKind(SyntaxKind.InterpolatedStringExpression))
                    return true;
                node = node.Parent;
            }

            // Trailing comment trivia on this line
            foreach (var trivia in token.TrailingTrivia)
            {
                if (IsCommentKind(trivia.Kind()) && trivia.Span.IntersectsWith(line.Span))
                    return true;
            }
        }

        // Step 2: line-starting comments are stored as leading trivia of the NEXT token.
        // Use FindToken at the first non-whitespace position on the line to find that token.
        var lineStr    = line.ToString();
        var wsLen      = lineStr.Length - lineStr.TrimStart().Length;
        if (wsLen < lineStr.Length)
        {
            var firstNonWs = line.Start + wsLen;
            var nextToken  = root.FindToken(firstNonWs, findInsideTrivia: true);
            foreach (var trivia in nextToken.LeadingTrivia)
            {
                if (IsCommentKind(trivia.Kind()) && trivia.Span.IntersectsWith(line.Span))
                    return true;
            }
        }

        return false;
    }

    private static bool IsCommentKind(SyntaxKind kind) =>
        kind is SyntaxKind.SingleLineCommentTrivia
             or SyntaxKind.MultiLineCommentTrivia
             or SyntaxKind.SingleLineDocumentationCommentTrivia
             or SyntaxKind.MultiLineDocumentationCommentTrivia;

    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple        => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified  => qualified.Right.Identifier.ValueText,
        _                              => string.Empty,
    };
}
