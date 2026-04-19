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
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(typeName);
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var lineSpan = text.Lines[lineNumber - 1].Span;
        return tree.GetRoot()
            .DescendantNodes(lineSpan)
            .OfType<ObjectCreationExpressionSyntax>()
            .Any(n => GetSimpleTypeName(n.Type).Equals(typeName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> when the character at the given 1-based <paramref name="lineNumber"/>
    /// and 0-based <paramref name="columnOffset"/> falls inside comment trivia or a
    /// string/interpolated-string literal token. Checks the specific match position rather
    /// than the entire line so that code like
    /// <c>if (x == 0.0) throw new Exception("msg")</c> is never suppressed just because
    /// a string literal exists elsewhere on the line.
    /// </summary>
    public static bool IsInCommentOrStringLiteral(SyntaxTree tree, int lineNumber, int columnOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var line = text.Lines[lineNumber - 1];
        if (columnOffset < 0) columnOffset = 0;
        if (line.Span.IsEmpty) return false;
        var position = line.Start + Math.Min(columnOffset, line.Span.Length - 1);

        var root  = tree.GetRoot();
        var token = root.FindToken(position, findInsideTrivia: true);

        // Direct string literal token at this position
        if (token.IsKind(SyntaxKind.StringLiteralToken)          ||
            token.IsKind(SyntaxKind.InterpolatedStringTextToken) ||
            token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken))
            return true;

        // Ancestor is a string expression node
        var node = token.Parent;
        while (node is not null)
        {
            if (node.IsKind(SyntaxKind.StringLiteralExpression)   ||
                node.IsKind(SyntaxKind.InterpolatedStringExpression))
                return true;
            node = node.Parent;
        }

        // Check if position falls inside comment trivia on this token
        foreach (var trivia in token.LeadingTrivia)
        {
            if (IsCommentKind(trivia.Kind()) && trivia.FullSpan.Contains(position))
                return true;
        }
        foreach (var trivia in token.TrailingTrivia)
        {
            if (IsCommentKind(trivia.Kind()) && trivia.FullSpan.Contains(position))
                return true;
        }

        // Also check the preceding token's trailing trivia (handles end-of-line comments
        // where FindToken returns the token AFTER the comment, not the one before it)
        var prevToken = token.GetPreviousToken();
        foreach (var trivia in prevToken.TrailingTrivia)
        {
            if (IsCommentKind(trivia.Kind()) && trivia.FullSpan.Contains(position))
                return true;
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
