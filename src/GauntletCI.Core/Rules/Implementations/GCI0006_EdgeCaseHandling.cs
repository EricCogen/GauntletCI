// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0006 – Edge Case Handling
/// Detects potential null dereferences and missing validation in added code.
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
                               l.Content.Contains("!= null", StringComparison.Ordinal));

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
                if (!paramSection.Contains("string ", StringComparison.Ordinal) &&
                    !paramSection.Contains("object ", StringComparison.Ordinal)) continue;

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
