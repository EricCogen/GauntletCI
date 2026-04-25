// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0006 – Edge Case Handling
/// Detects potential null dereferences and missing validation in added code.
/// Boundary with GCI0043 (Nullability and Type Safety): GCI0043 detects as-casts without null checks
/// but suppresses when the same line also has a .Value access, deferring to GCI0006 as the
/// authoritative reporter for that combined pattern.
/// </summary>
public class GCI0006_EdgeCaseHandling : RuleBase
{
    public override string Id => "GCI0006";
    public override string Name => "Edge Case Handling";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckNullDereferences(diff, findings);
        CheckMissingParameterValidation(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckNullDereferences(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                if (!content.Contains(".Value", StringComparison.Ordinal)) continue;

                // Skip comment lines — .Value in a comment is not executable code
                if (content.TrimStart().StartsWith("//")) continue;

                // Check preceding lines for null guard
                int start = Math.Max(0, i - 5);
                bool hasGuard = addedLines[start..i]
                    .Any(l => l.Content.Contains("null", StringComparison.Ordinal) ||
                               l.Content.Contains("HasValue", StringComparison.Ordinal) ||
                               l.Content.Contains("is not null", StringComparison.Ordinal) ||
                               l.Content.Contains("!= null", StringComparison.Ordinal) ||
                               IsSuccessGuardFor(content, l.Content));

                if (!hasGuard)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Potential null dereference via .Value access in {file.NewPath}",
                        evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                        whyItMatters: "Accessing .Value on a nullable without a null check will throw InvalidOperationException at runtime.",
                        suggestedAction: "Add a null check or use ?.Value with null-coalescing before accessing .Value.",
                        confidence: Confidence.Medium,
                        line: addedLines[i]));
                    break; // one finding per file
                }
            }
        }
    }

    private void CheckMissingParameterValidation(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            // Test helpers do not need null guards — skip test files entirely
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                // Only flag public or protected methods — private/internal callers are controlled
                if (!IsPublicOrProtectedSignature(content)) continue;

                // Check "string" or "object" in the parameter section, not just the return type
                var parenIdx = content.IndexOf('(');
                var paramSection = parenIdx >= 0 ? content[parenIdx..] : "";
                if (!HasNullableReferenceParam(paramSection)) continue;

                // Check next 5 lines for null/range validation
                int end = Math.Min(addedLines.Count, i + 6);
                bool hasValidation = addedLines[(i + 1)..end]
                    .Any(l => l.Content.Contains("null", StringComparison.Ordinal) ||
                               l.Content.Contains("ArgumentNull", StringComparison.Ordinal) ||
                               l.Content.Contains("ArgumentException", StringComparison.Ordinal) ||
                               l.Content.Contains("throw", StringComparison.Ordinal));

                if (!hasValidation)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"New method parameter(s) added without apparent null/range validation in {file.NewPath}",
                        evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                        whyItMatters: "Unvalidated parameters can lead to NullReferenceException or incorrect behaviour deeper in the call stack.",
                        suggestedAction: "Add ArgumentNullException.ThrowIfNull() or similar guard at the top of the method.",
                        confidence: Confidence.Medium,
                        line: addedLines[i]));
                    break;
                }
            }
        }
    }

    private static bool HasNullableReferenceParam(string paramSection)
    {
        // Walk character by character, tracking generic depth so we skip type arguments
        // like Dictionary<string?, int> and only match top-level parameters.
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            foreach (var keyword in new[] { "string?", "object?" })
            {
                if (i + keyword.Length > paramSection.Length) continue;
                if (!paramSection.AsSpan(i).StartsWith(keyword, StringComparison.Ordinal)) continue;

                // Leading boundary: must be preceded by a non-identifier char
                bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                if (!leadOk) continue;

                // Trailing boundary: must be followed by a non-identifier char
                int after = i + keyword.Length;
                bool trailOk = after >= paramSection.Length ||
                               paramSection[after] is ' ' or '[' or ',' or ')' or '<';
                if (trailOk) return true;
            }
        }
        return false;
    }

    // Returns true only when the .Success check in guardLine refers to the same root
    // identifier as the .Value access in valueLine (e.g. "match.Success" guards "match.Groups[1].Value").
    private static bool IsSuccessGuardFor(string valueLine, string guardLine)
    {
        int valIdx = valueLine.IndexOf(".Value", StringComparison.Ordinal);
        if (valIdx <= 0) return false;

        // Walk backward from the dot to collect the expression chain (e.g. "match.Groups[1]")
        int start = valIdx - 1;
        while (start > 0 && valueLine[start - 1] is char pc &&
               (char.IsLetterOrDigit(pc) || pc is '_' or '.' or '[' or ']'))
            start--;

        var expr = valueLine[start..valIdx]; // e.g. "match.Groups[1]"

        // Extract the root identifier — the first segment before '.' or '['
        int boundary = expr.IndexOfAny(['.', '[']);
        var root = boundary > 0 ? expr[..boundary] : expr;
        root = new string(root.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        return root.Length > 0 && guardLine.Contains(root + ".Success", StringComparison.Ordinal);
    }

    private static bool IsPublicOrProtectedSignature(string line)
    {
        var t = line.Trim();
        return t.Contains('(') && t.Contains(')') &&
               (t.StartsWith("public ", StringComparison.Ordinal) ||
                t.StartsWith("protected ", StringComparison.Ordinal));
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        var ca1062 = staticAnalysis.Diagnostics.Where(d => d.Id == "CA1062");
        foreach (var diag in ca1062)
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0006",
                RuleName = "Edge Case Handling",
                Summary = $"CA1062: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a parameter that is not validated before use.",
                SuggestedAction = "Validate all reference parameters before use.",
                Confidence = Confidence.Medium,
            });
        }
    }
}
