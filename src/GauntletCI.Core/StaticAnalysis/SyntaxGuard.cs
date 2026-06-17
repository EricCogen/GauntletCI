// SPDX-License-Identifier: Elastic-2.0
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.InteropServices;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Provides syntax-tree-based guards for reducing false positives in regex-based rules.
/// All methods operate on a pre-parsed <see cref="SyntaxTree"/> and are intentionally
/// lightweight: no semantic model, no compilation, no disk I/O.
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

        var root = tree.GetRoot();
        var token = root.FindToken(position, findInsideTrivia: true);

        // Direct string literal token at this position
        if (token.IsKind(SyntaxKind.StringLiteralToken) ||
            token.IsKind(SyntaxKind.InterpolatedStringTextToken) ||
            token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) ||
            token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
            return true;

        // Ancestor is a plain string literal expression.
        // Deliberately excludes InterpolatedStringExpression: the {…} holes are
        // live code, so code that appears inside an interpolated expression should
        // still be flagged (e.g. $"{new Random().Next()}" is a real finding).
        var node = token.Parent;
        while (node is not null)
        {
            if (node.IsKind(SyntaxKind.StringLiteralExpression)) return true;
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

    /// <summary>
    /// Returns <c>true</c> when a plain string literal on the line satisfies <paramref name="predicate"/>.
    /// </summary>
    public static bool HasStringLiteralMatching(SyntaxTree tree, int lineNumber, Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(predicate);
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var lineSpan = text.Lines[lineNumber - 1].Span;
        return tree.GetRoot()
            .DescendantNodes(lineSpan)
            .OfType<LiteralExpressionSyntax>()
            .Where(n => n.IsKind(SyntaxKind.StringLiteralExpression))
            .Any(n => predicate(n.Token.ValueText));
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="columnOffset"/> falls on a
    /// <see cref="MemberAccessExpressionSyntax"/> whose member name matches <paramref name="memberName"/>.
    /// </summary>
    public static bool HasMemberAccessAtPosition(
        SyntaxTree tree,
        int lineNumber,
        string memberName,
        int columnOffset)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(memberName);
        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count) return false;

        var line = text.Lines[lineNumber - 1];
        if (line.Span.IsEmpty) return false;
        if (columnOffset < 0) columnOffset = 0;
        var position = line.Start + Math.Min(columnOffset, line.Span.Length - 1);

        for (var node = tree.GetRoot().FindNode(new TextSpan(position, 0)); node is not null; node = node.Parent)
        {
            if (node is not MemberAccessExpressionSyntax memberAccess)
                continue;

            if (!memberAccess.Name.Identifier.Text.Equals(memberName, StringComparison.Ordinal))
                continue;

            if (!memberAccess.Name.Span.Contains(position))
                continue;

            return true;
        }

        return false;
    }

    private static readonly HashSet<string> SystemIoFileSyncMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReadAllText",
        "ReadAllLines",
        "WriteAllText",
        "WriteAllLines",
        "Copy",
        "ReadAllBytes",
        "WriteAllBytes",
    };

    /// <summary>
    /// Returns <c>true</c> when the line contains a confirmed <c>System.IO.File</c> synchronous
    /// method invocation matching <paramref name="methodName"/>.
    /// Uses syntax structure first, then a lightweight semantic bind when compilation succeeds.
    /// </summary>
    public static bool HasSystemIoFileSyncInvocation(SyntaxTree tree, int lineNumber, string methodName)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(methodName);
        if (!SystemIoFileSyncMethods.Contains(methodName))
            return false;

        var text = tree.GetText();
        if (lineNumber < 1 || lineNumber > text.Lines.Count)
            return false;

        var lineSpan = text.Lines[lineNumber - 1].Span;
        foreach (var invocation in tree.GetRoot().DescendantNodes(lineSpan).OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            if (!memberAccess.Name.Identifier.Text.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsSystemIoFileExpression(memberAccess.Expression))
                continue;

            if (!IsSemanticallySystemIoFile(tree, invocation))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsSystemIoFileExpression(ExpressionSyntax? expression) =>
        expression switch
        {
            IdentifierNameSyntax { Identifier.Text: "File" } => true,
            QualifiedNameSyntax qualified when qualified.Right.Identifier.Text == "File" =>
                IsSystemIoNamespace(qualified.Left),
            AliasQualifiedNameSyntax aliased => IsSystemIoFileExpression(aliased.Name),
            MemberAccessExpressionSyntax member when member.Name.Identifier.Text == "File" =>
                IsSystemIoNamespace(member.Expression),
            _ => false,
        };

    private static bool IsSystemIoNamespace(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        return text.Equals("System.IO", StringComparison.Ordinal)
            || text.EndsWith(".System.IO", StringComparison.Ordinal);
    }

    private static bool IsSemanticallySystemIoFile(SyntaxTree tree, InvocationExpressionSyntax invocation)
    {
        try
        {
            var compilation = CreateMiniCompilation(tree);
            var model = compilation.GetSemanticModel(tree);
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                return true;

            var containingType = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return containingType is "global::System.IO.File" or "System.IO.File";
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static CSharpCompilation CreateMiniCompilation(SyntaxTree tree)
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var references = new List<MetadataReference>();
        foreach (var dll in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        return CSharpCompilation.Create(
            assemblyName: "GauntletCI.SyntaxGuard",
            syntaxTrees: [tree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => GetSimpleTypeName(aliased.Name),
        _ => string.Empty,
    };
}
