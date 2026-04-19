// SPDX-License-Identifier: Elastic-2.0
using Microsoft.CodeAnalysis;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Holds pre-parsed Roslyn <see cref="SyntaxTree"/> instances for each C# file
/// that was analyzed in this run. Exposes guard helpers used by rules to reduce
/// false positives without requiring a full semantic model.
/// <para>
/// Pass-through semantics when no tree is available for a file:
/// <list type="bullet">
///   <item><description><see cref="IsConfirmedObjectCreation"/> returns <c>true</c> (don't suppress — no evidence to filter).</description></item>
///   <item><description><see cref="IsInCommentOrStringLiteral"/> returns <c>false</c> (don't suppress — no evidence to suppress).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class SyntaxContext
{
    private readonly Dictionary<string, SyntaxTree> _trees;

    public SyntaxContext(Dictionary<string, SyntaxTree> trees) =>
        _trees = trees ?? throw new ArgumentNullException(nameof(trees));

    /// <summary>Number of files for which a syntax tree is available.</summary>
    public int TreeCount => _trees.Count;

    /// <summary>
    /// Returns <c>true</c> when Roslyn confirms an <c>ObjectCreationExpression</c>
    /// of type <paramref name="typeName"/> exists on the given line, or when no
    /// syntax tree is available for the file (pass-through).
    /// </summary>
    public bool IsConfirmedObjectCreation(string filePath, int lineNumber, string typeName)
    {
        if (!TryGetTree(filePath, out var tree)) return true;
        return SyntaxGuard.HasObjectCreation(tree!, lineNumber, typeName);
    }

    /// <summary>
    /// Returns <c>true</c> when the line falls inside a comment or string literal.
    /// Returns <c>false</c> (don't suppress) when no syntax tree is available.
    /// </summary>
    public bool IsInCommentOrStringLiteral(string filePath, int lineNumber)
    {
        if (!TryGetTree(filePath, out var tree)) return false;
        return SyntaxGuard.IsInCommentOrStringLiteral(tree!, lineNumber);
    }

    private bool TryGetTree(string filePath, out SyntaxTree? tree)
    {
        if (_trees.TryGetValue(filePath, out tree)) return true;

        // Normalize separators for cross-platform path matching
        var normalized = filePath.Replace('\\', '/');
        foreach (var kvp in _trees)
        {
            if (kvp.Key.Replace('\\', '/').EndsWith(normalized, StringComparison.OrdinalIgnoreCase))
            {
                tree = kvp.Value;
                return true;
            }
        }

        return false;
    }
}
