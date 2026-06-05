// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Analysis;

/// <summary>
/// Detects removed null/validation guards with continued or new uses of the guarded symbol (PG-RELATION / RC-3).
/// </summary>
internal static class GuardDeletionAnalyzer
{
    private static readonly Regex MethodDeclarationRegex =
        new(@"\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:override\s+)?(?:async\s+)?[\w<>\[\]?]+\s+(\w+)\s*\(",
            RegexOptions.Compiled);

    private static readonly Regex GuardIfRegex =
        new(@"if\s*\(\s*(?<ident>[A-Za-z_][A-Za-z0-9_]*)\s*(?:==|is)\s*null\s*\)", RegexOptions.Compiled);

    private static readonly Regex ThrowIfNullRegex =
        new(@"(?:ArgumentNullException\.)?ThrowIfNull\s*\(\s*(?<ident>[A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.Compiled);

    internal sealed record GuardRemoval(
        string ClassName,
        string MethodName,
        int LineNumber,
        int LineIndex,
        string GuardedSymbol,
        string GuardText,
        bool IsRemovedLine);

    internal sealed record SymbolUse(
        string ClassName,
        string MethodName,
        int LineNumber,
        int LineIndex,
        string Symbol,
        string UseText,
        bool IsAddedLine);

    internal static IReadOnlyList<(GuardRemoval Guard, SymbolUse Use)> FindRemoteUsesAfterGuardDeletion(DiffFile file)
    {
        var orderedLines = file.Hunks
            .SelectMany(h => h.Lines)
            .Where(l => l.Kind is DiffLineKind.Added or DiffLineKind.Removed or DiffLineKind.Context)
            .ToList();

        string? currentClass = null;
        string? currentMethod = null;
        var guards = new List<GuardRemoval>();
        var uses = new List<SymbolUse>();

        for (var index = 0; index < orderedLines.Count; index++)
        {
            var line = orderedLines[index];
            var content = line.Content;
            var effectiveKind = GetEffectiveKind(line);
            var classMatch = Regex.Match(NormalizeDiffLine(content), @"\b(?:class|record|struct)\s+(\w+)\b");
            if (classMatch.Success)
                currentClass = classMatch.Groups[1].Value!;

            var methodMatch = MethodDeclarationRegex.Match(NormalizeDiffLine(content));
            if (methodMatch.Success)
                currentMethod = methodMatch.Groups[1].Value!;

            if (currentClass is null || currentMethod is null)
                continue;

            foreach (var guard in ExtractRemovedGuards(content, effectiveKind))
            {
                guards.Add(new GuardRemoval(
                    currentClass,
                    currentMethod,
                    line.LineNumber,
                    index,
                    guard,
                    NormalizeDiffLine(content),
                    IsRemovedLine: true));
            }

            if (effectiveKind is EffectiveLineKind.Added or EffectiveLineKind.Context)
            {
                foreach (var guard in ExtractGuardSymbols(NormalizeDiffLine(content)))
                {
                    guards.Add(new GuardRemoval(
                        currentClass,
                        currentMethod,
                        line.LineNumber,
                        index,
                        guard,
                        NormalizeDiffLine(content),
                        IsRemovedLine: false));
                }
            }

            foreach (var symbol in ExtractSymbolUses(content))
            {
                uses.Add(new SymbolUse(
                    currentClass,
                    currentMethod,
                    line.LineNumber,
                    index,
                    symbol,
                    NormalizeDiffLine(content),
                    effectiveKind == EffectiveLineKind.Added));
            }
        }

        var mismatches = new List<(GuardRemoval, SymbolUse)>();

        foreach (var guard in guards.Where(g => g.IsRemovedLine))
        {
            var replacementGuard = guards.Any(g =>
                !g.IsRemovedLine &&
                g.MethodName == guard.MethodName &&
                g.GuardedSymbol.Equals(guard.GuardedSymbol, StringComparison.Ordinal) &&
                g.LineIndex >= guard.LineIndex - 2 &&
                g.LineIndex <= guard.LineIndex + 6);

            if (replacementGuard)
                continue;

            var remoteUse = uses
                .Where(u =>
                    u.MethodName == guard.MethodName &&
                    u.Symbol.Equals(guard.GuardedSymbol, StringComparison.Ordinal) &&
                    u.LineIndex > guard.LineIndex)
                .OrderByDescending(u => u.IsAddedLine)
                .ThenBy(u => u.LineIndex)
                .FirstOrDefault();

            if (remoteUse is not null)
                mismatches.Add((guard, remoteUse));
        }

        return mismatches;
    }

    private static IEnumerable<string> ExtractRemovedGuards(string content, EffectiveLineKind kind)
    {
        if (kind != EffectiveLineKind.Removed)
            yield break;

        foreach (var guard in ExtractGuardSymbols(NormalizeDiffLine(content)))
            yield return guard;
    }

    private enum EffectiveLineKind
    {
        Context,
        Added,
        Removed,
    }

    private static EffectiveLineKind GetEffectiveKind(DiffLine line)
    {
        if (line.Kind == DiffLineKind.Added)
            return EffectiveLineKind.Added;

        if (line.Kind == DiffLineKind.Removed)
            return EffectiveLineKind.Removed;

        var trimmed = line.Content.TrimStart();
        if (trimmed.StartsWith('+'))
            return EffectiveLineKind.Added;

        if (trimmed.StartsWith('-'))
            return EffectiveLineKind.Removed;

        return EffectiveLineKind.Context;
    }

    private static IEnumerable<string> ExtractGuardSymbols(string trimmed)
    {
        foreach (Match match in GuardIfRegex.Matches(trimmed))
            yield return match.Groups["ident"].Value!;

        foreach (Match match in ThrowIfNullRegex.Matches(trimmed))
            yield return match.Groups["ident"].Value!;
    }

    private static IEnumerable<string> ExtractSymbolUses(string content)
    {
        var trimmed = NormalizeDiffLine(content);
        if (trimmed.StartsWith("if", StringComparison.Ordinal) && trimmed.Contains("null", StringComparison.Ordinal))
            yield break;

        foreach (Match match in Regex.Matches(trimmed, @"\b([A-Za-z_][A-Za-z0-9_]*)\.(?!\.)"))
        {
            var symbol = match.Groups[1].Value!;
            if (IsKeyword(symbol))
                continue;

            yield return symbol;
        }

        foreach (Match match in Regex.Matches(trimmed, @"\b([A-Za-z_][A-Za-z0-9_]*)\.Value\b"))
        {
            var symbol = match.Groups[1].Value!;
            if (!IsKeyword(symbol))
                yield return symbol;
        }
    }

    private static bool IsKeyword(string symbol) =>
        symbol is "if" or "for" or "foreach" or "while" or "return" or "var" or "new" or "this" or "base";

    private static string NormalizeDiffLine(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed[0] is '+' or '-' or '\\')
            trimmed = trimmed[1..].TrimStart();

        return trimmed;
    }
}
