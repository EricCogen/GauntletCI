// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

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
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        int braceDepth = 0;
        bool inGetter = false;
        int getterExitDepth = -1;
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
                inGetter = true;
                expectGetterBrace = false;
            }
            else
            {
                expectGetterBrace = false;
            }

            // Capture pure context state before brace counting
            bool inPureContext = inGetter || seenPure;

            // Count braces
            foreach (char c in content)
            {
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
            }

            // Exit getter when depth returns to entry level
            if (inGetter && braceDepth <= getterExitDepth)
                inGetter = false;

            // Check for mutations in pure context (added lines only)
            if (line.Kind == DiffLineKind.Added && inPureContext && HasAssignment(trimmed))
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

    private static bool HasAssignment(string content)
    {
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] != '=') continue;
            char prev = i > 0 ? content[i - 1] : '\0';
            char next = i + 1 < content.Length ? content[i + 1] : '\0';
            // Skip ==, !=, =>, <=, >=
            if (prev is '=' or '!' or '<' or '>') continue;
            if (next is '=' or '>') continue;
            return true;
        }
        return false;
    }
}
