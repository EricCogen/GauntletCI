// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0031 – Boundary Drift
/// Fires when comparison operators against numeric literals are added without test coverage of those values.
/// </summary>
public class GCI0031_BoundaryDrift : RuleBase
{
    public override string Id => "GCI0031";
    public override string Name => "Boundary Drift";

    private static readonly Regex BoundaryRegex =
        new(@"(?<![<>!=])[<>]=?\s*(\d+)", RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        var nonTestFiles = diff.Files.Where(f =>
            !f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
            !f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase)).ToList();

        var testFiles = diff.Files.Where(f =>
            f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase)).ToList();

        var foundLiterals = new Dictionary<string, (int lineNumber, string content)>();
        foreach (var file in nonTestFiles)
        {
            foreach (var line in file.AddedLines)
            {
                var matches = BoundaryRegex.Matches(line.Content);
                foreach (Match m in matches)
                {
                    var literal = m.Groups[1].Value;
                    if (!foundLiterals.ContainsKey(literal))
                        foundLiterals[literal] = (line.LineNumber, line.Content.Trim());
                }
            }
        }

        if (foundLiterals.Count == 0) return Task.FromResult(findings);

        var testLines = testFiles
            .SelectMany(f => f.Hunks.SelectMany(h => h.Lines))
            .Select(l => l.Content)
            .ToList();

        foreach (var (literal, (lineNumber, content)) in foundLiterals)
        {
            bool hasCoverage = testLines.Any(tl =>
                tl.Contains(literal) &&
                (tl.Contains("InlineData") || tl.Contains("Assert") || tl.Contains("Should()")));

            if (!hasCoverage)
            {
                findings.Add(CreateFinding(
                    summary: $"Boundary value {literal} added via comparison operator with no matching test evidence in diff.",
                    evidence: $"Line {lineNumber}: {content}",
                    whyItMatters: "Off-by-one errors at boundaries are one of the most common sources of bugs. Without tests at the exact boundary value, correctness cannot be verified.",
                    suggestedAction: $"Add an xUnit [InlineData({literal})] or equivalent test that exercises this boundary value.",
                    confidence: Confidence.Medium));
            }
        }

        return Task.FromResult(findings);
    }
}
