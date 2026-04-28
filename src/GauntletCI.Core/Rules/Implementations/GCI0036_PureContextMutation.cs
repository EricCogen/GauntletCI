// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0036 – Pure Context Mutation
/// Detects assignment operators inside property getter blocks or methods decorated with [Pure].
/// </summary>
public class GCI0036_PureContextMutation : RuleBase
{
    public override string Id => "GCI0036";
    public override string Name => "Pure Context Mutation";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckPureContextMutations(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckPureContextMutations(DiffFile file, List<Finding> findings)
    {
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) return;

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        int braceDepth = 0;
        bool inGetter = false;
        int getterExitDepth = -1;
        int getterStartIdx = -1;
        bool expectGetterBrace = false;
        bool seenPure = false;
        int pureLineIdx = -1;

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            var content = line.Content;
            var trimmed = content.Trim();

            // Track [Pure]
            if (trimmed.Contains("[Pure]"))
            {
                seenPure = true;
                pureLineIdx = i;
            }
            if (seenPure && i - pureLineIdx > 5)
                seenPure = false;

            // Detect getter with inline brace
            if (trimmed.StartsWith("get {") || trimmed.Contains(" get {"))
            {
                getterExitDepth = braceDepth;
                getterStartIdx = i;
                inGetter = true;
                expectGetterBrace = false;
            }
            // Detect getter on its own line (brace on next line)
            else if (trimmed == "get" || (trimmed.Length > 4 && trimmed.EndsWith(" get") && !trimmed.Contains("{")))
            {
                expectGetterBrace = true;
            }
            // Detect deferred getter brace
            else if (expectGetterBrace && (trimmed == "{" || trimmed.StartsWith("{ ")))
            {
                getterExitDepth = braceDepth;
                getterStartIdx = i;
                inGetter = true;
                expectGetterBrace = false;
            }
            else
            {
                expectGetterBrace = false;
            }

            // Capture pure context state before brace counting
            bool inPureContext = inGetter || seenPure;
            int contextStartIdx = inGetter ? getterStartIdx : (seenPure ? pureLineIdx : -1);

            // Count braces
            foreach (char c in content)
            {
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
            }

            // Exit getter when depth returns to entry level
            if (inGetter && braceDepth <= getterExitDepth)
            {
                inGetter = false;
                getterStartIdx = -1;
            }

            // Check for mutations in pure context (added lines only)
            if (line.Kind == DiffLineKind.Added && inPureContext && IsFieldOrPropertyAssignment(trimmed)
                && !IsNullGuardedAssignment(allLines, i, trimmed)
                && !IsLocalVariableInScope(allLines, contextStartIdx, i, trimmed))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Assignment in getter or [Pure] method in {file.NewPath} — mutation in a pure context.",
                    evidence: $"Line {line.LineNumber}: {trimmed}",
                    whyItMatters: "Property getters and [Pure]-annotated methods are expected to be side-effect free. Mutations break this contract and can cause subtle bugs with lazy initialization, caching, or framework reflection.",
                    suggestedAction: "Move state mutations to setter, constructor, or a dedicated method. If lazy init is intended, use Lazy<T> or Interlocked.",
                    confidence: Confidence.High,
                    line: line));
            }
        }
    }

    /// <summary>
    /// Returns true when the assignment on this line is preceded within 20 lines by a null check
    /// on the same field — the lazy-initialization pattern (check-then-assign) is intentional.
    /// The window is 20 lines to cover nested double-check-lock patterns.
    /// </summary>
    private static bool IsNullGuardedAssignment(List<DiffLine> allLines, int idx, string trimmed)
    {
        int eqIdx = FindAssignmentIndex(trimmed);
        if (eqIdx < 0) return false;

        var rawLhs = trimmed[..eqIdx].TrimEnd('+', '-', '*', '/', '%', '|', '&', '^', ' ').Trim();
        if (string.IsNullOrEmpty(rawLhs)) return false;

        // Use just the simple name (strip `this.` prefix) for the null-check search
        var lhsName = rawLhs.Contains('.')
            ? rawLhs[(rawLhs.LastIndexOf('.') + 1)..]
            : rawLhs;
        if (string.IsNullOrEmpty(lhsName) || lhsName.Contains(' ')) return false;

        int scanned = 0;
        for (int j = idx - 1; j >= 0 && scanned < 20; j--)
        {
            var prev = allLines[j].Content.Trim();
            if (string.IsNullOrEmpty(prev)) continue;
            scanned++;

            if (prev.Contains(lhsName, StringComparison.Ordinal) &&
                (prev.Contains("== null",   StringComparison.Ordinal) ||
                 prev.Contains("is null",   StringComparison.Ordinal) ||
                 prev.Contains("!= null",   StringComparison.Ordinal) ||
                 prev.Contains("is not null", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFieldOrPropertyAssignment(string trimmed)    {
        ArgumentNullException.ThrowIfNull(trimmed);
        // Skip local variable declarations and loop variables
        if (trimmed.StartsWith("var ", StringComparison.Ordinal)) return false;
        if (trimmed.StartsWith("for ", StringComparison.Ordinal) ||
            trimmed.StartsWith("for(", StringComparison.Ordinal)) return false;

        int eqIdx = FindAssignmentIndex(trimmed);
        if (eqIdx < 0) return false;

        // Strip compound-assignment operator character (+=, -=, etc.) then trim spaces
        // so "total += x" → lhs = "total" (no space → real field mutation)
        var lhs = trimmed[..eqIdx].TrimEnd('+', '-', '*', '/', '%', '|', '&', '^', ' ');

        // If LHS still contains a space it's a type declaration (e.g. "int x", "Dictionary<K,V> result")
        return !lhs.Contains(' ');
    }

    /// <summary>
    /// Returns true when the LHS name was declared as a local variable within the getter/pure
    /// scope that starts at <paramref name="scopeStart"/>. Scans lines [scopeStart, idx) for
    /// "TypeName varName" or "var varName" declaration patterns. Skips private-field naming
    /// conventions (_name, m_name) since those are always fields and never locals.
    /// </summary>
    private static bool IsLocalVariableInScope(
        List<DiffLine> allLines, int scopeStart, int idx, string trimmed)
    {
        if (scopeStart < 0) return false;

        int eqIdx = FindAssignmentIndex(trimmed);
        if (eqIdx < 0) return false;

        var rawLhs = trimmed[..eqIdx].TrimEnd('+', '-', '*', '/', '%', '|', '&', '^', ' ').Trim();
        if (rawLhs.Length == 0) return false;

        // Dotted (this.x) or indexed (arr[i]) — can't be a plain local
        if (rawLhs.Contains('.') || rawLhs.Contains('[') || rawLhs.Contains(')')) return false;

        // Private-field naming conventions → always a field, never a local
        if (rawLhs.StartsWith("_", StringComparison.Ordinal) ||
            rawLhs.StartsWith("m_", StringComparison.Ordinal)) return false;

        // Search within the getter/pure scope for "Type varName" or "var varName"
        for (int j = scopeStart; j < idx; j++)
        {
            var content = allLines[j].Content;
            if (string.IsNullOrWhiteSpace(content)) continue;

            int pos = -1;
            while ((pos = content.IndexOf(rawLhs, pos + 1, StringComparison.Ordinal)) >= 0)
            {
                // Name must be preceded by a space (type separator)
                if (pos == 0 || content[pos - 1] != ' ') continue;

                // Name must be followed by space, =, ;, or , (end of declarator)
                int afterPos = pos + rawLhs.Length;
                if (afterPos < content.Length &&
                    content[afterPos] is not (' ' or '=' or ';' or ',')) continue;

                // What precedes the space must end with a type-name character (letter, digit, >, ], ?)
                var before = content[..pos].TrimEnd();
                if (before.Length == 0) continue;
                char lastChar = before[^1];
                if (char.IsLetterOrDigit(lastChar) || lastChar is '>' or ']' or '?')
                    return true;
            }
        }
        return false;
    }

    private static int FindAssignmentIndex(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] != '=') continue;
            char prev = i > 0 ? content[i - 1] : '\0';
            char next = i + 1 < content.Length ? content[i + 1] : '\0';
            if (prev is '=' or '!' or '<' or '>') continue;
            if (next is '=' or '>') continue;
            return i;
        }
        return -1;
    }
}
