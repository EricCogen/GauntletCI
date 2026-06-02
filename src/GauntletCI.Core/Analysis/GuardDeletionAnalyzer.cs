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
            var normalized = NormalizeDiffLine(content);

            var classMatch = Regex.Match(normalized, @"\b(?:class|record|struct)\s+(\w+)\b");
            if (classMatch.Success && TryGetCapture(classMatch, 1, out var className))
                currentClass = className;

            var methodMatch = MethodDeclarationRegex.Match(normalized);
            if (methodMatch.Success && TryGetCapture(methodMatch, 1, out var methodName))
                currentMethod = methodName;

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
                    normalized,
                    IsRemovedLine: true));
            }

            if (effectiveKind is EffectiveLineKind.Added or EffectiveLineKind.Context)
            {
                foreach (var guard in ExtractGuardSymbols(normalized))
                {
                    guards.Add(new GuardRemoval(
                        currentClass,
                        currentMethod,
                        line.LineNumber,
                        index,
                        guard,
                        normalized,
                        IsRemovedLine: false));
                }
            }

            if (effectiveKind is EffectiveLineKind.Added or EffectiveLineKind.Context)
            {
                foreach (var symbol in ExtractSymbolUses(content))
                {
                    uses.Add(new SymbolUse(
                        currentClass,
                        currentMethod,
                        line.LineNumber,
                        index,
                        symbol,
                        normalized,
                        effectiveKind == EffectiveLineKind.Added));
                }
            }
        }

        var mismatches = new List<(GuardRemoval, SymbolUse)>();

        foreach (var guard in guards)
        {
            if (!guard.IsRemovedLine)
                continue;

            var remoteUse = FindRemoteUse(guard, uses);
            if (remoteUse is null)
                continue;

            if (HasReplacementGuardBeforeUse(guard, remoteUse, guards))
                continue;

            mismatches.Add((guard, remoteUse));
        }

        return mismatches;
    }

    private static SymbolUse? FindRemoteUse(GuardRemoval guard, List<SymbolUse> uses)
    {
        SymbolUse? best = null;
        foreach (var use in uses)
        {
            if (!use.ClassName.Equals(guard.ClassName, StringComparison.Ordinal))
                continue;

            if (!use.MethodName.Equals(guard.MethodName, StringComparison.Ordinal))
                continue;

            if (!use.Symbol.Equals(guard.GuardedSymbol, StringComparison.Ordinal))
                continue;

            if (use.LineIndex <= guard.LineIndex)
                continue;

            if (best is null ||
                (use.IsAddedLine && !best.IsAddedLine) ||
                (use.IsAddedLine == best.IsAddedLine && use.LineIndex < best.LineIndex))
            {
                best = use;
            }
        }

        return best;
    }

    private static bool HasReplacementGuardBeforeUse(
        GuardRemoval removedGuard,
        SymbolUse remoteUse,
        List<GuardRemoval> guards)
    {
        foreach (var candidate in guards)
        {
            if (candidate.IsRemovedLine)
                continue;

            if (!candidate.ClassName.Equals(removedGuard.ClassName, StringComparison.Ordinal))
                continue;

            if (!candidate.MethodName.Equals(removedGuard.MethodName, StringComparison.Ordinal))
                continue;

            if (!candidate.GuardedSymbol.Equals(removedGuard.GuardedSymbol, StringComparison.Ordinal))
                continue;

            if (candidate.LineIndex <= removedGuard.LineIndex)
                continue;

            if (candidate.LineIndex >= remoteUse.LineIndex)
                continue;

            return true;
        }

        return false;
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
        {
            if (TryGetCapture(match, "ident", out var ident))
                yield return ident;
        }

        foreach (Match match in ThrowIfNullRegex.Matches(trimmed))
        {
            if (TryGetCapture(match, "ident", out var ident))
                yield return ident;
        }
    }

    private static IEnumerable<string> ExtractSymbolUses(string content)
    {
        var trimmed = NormalizeDiffLine(content);
        if (trimmed.StartsWith("if", StringComparison.Ordinal) && trimmed.Contains("null", StringComparison.Ordinal))
            yield break;

        foreach (Match match in Regex.Matches(trimmed, @"\b([A-Za-z_][A-Za-z0-9_]*)\.(?!\.)"))
        {
            if (!TryGetCapture(match, 1, out var symbol) || IsKeyword(symbol))
                continue;

            yield return symbol;
        }

        foreach (Match match in Regex.Matches(trimmed, @"\b([A-Za-z_][A-Za-z0-9_]*)\.Value\b"))
        {
            if (!TryGetCapture(match, 1, out var symbol) || IsKeyword(symbol))
                continue;

            yield return symbol;
        }
    }

    private static bool TryGetCapture(Match match, int groupIndex, out string value)
    {
        value = string.Empty;
        if (!match.Success || groupIndex >= match.Groups.Count)
            return false;

        var group = match.Groups[groupIndex];
        if (!group.Success || string.IsNullOrEmpty(group.Value))
            return false;

        value = group.Value;
        return true;
    }

    private static bool TryGetCapture(Match match, string groupName, out string value)
    {
        value = string.Empty;
        if (!match.Success || !match.Groups[groupName].Success)
            return false;

        value = match.Groups[groupName].Value;
        return !string.IsNullOrEmpty(value);
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
