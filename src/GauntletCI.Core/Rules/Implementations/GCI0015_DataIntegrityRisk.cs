// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0015 – Data Integrity Risk
/// Detects unchecked casts, mass assignment without validation, and SQL IGNORE patterns.
/// </summary>
public class GCI0015_DataIntegrityRisk : RuleBase
{
    public override string Id => "GCI0015";
    public override string Name => "Data Integrity Risk";

    private static readonly string[] UncheckedCastPatterns = ["(int)", "(long)", "(decimal)", "(float)", "(short)"];
    private static readonly string[] SqlIgnorePatterns = ["INSERT IGNORE", "ON CONFLICT DO NOTHING", "INSERT OR IGNORE"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckMassAssignment(file, findings);
        }

        foreach (var line in diff.AllAddedLines)
        {
            CheckUncheckedCasts(line, findings);
            CheckSqlIgnore(line, findings);
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckMassAssignment(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        // Look for 3+ consecutive entity.Field = request.Field patterns
        int assignmentCount = 0;
        int firstLine = 0;

        for (int i = 0; i < addedLines.Count; i++)
        {
            var content = addedLines[i].Content.Trim();
            bool isFieldAssignment = content.Contains(".") &&
                                      content.Contains(" = ") &&
                                      content.EndsWith(';') &&
                                      !content.StartsWith("//");
            if (isFieldAssignment)
            {
                if (assignmentCount == 0) firstLine = addedLines[i].LineNumber;
                assignmentCount++;
            }
            else
            {
                if (assignmentCount >= 3)
                {
                    // Check if no null checks nearby
                    bool hasNullCheck = addedLines[Math.Max(0, i - assignmentCount - 2)..i]
                        .Any(l => l.Content.Contains("null", StringComparison.Ordinal) ||
                                  l.Content.Contains("ArgumentNull", StringComparison.Ordinal));
                    if (!hasNullCheck)
                    {
                        findings.Add(CreateFinding(
                            summary: $"Mass field assignment ({assignmentCount} assignments) without null validation in {file.NewPath}.",
                            evidence: $"Starting at line {firstLine} in {file.NewPath}",
                            whyItMatters: "Direct field assignment from user input without validation can lead to data corruption or over-posting attacks.",
                            suggestedAction: "Validate input with a DTO/ViewModel, use FluentValidation, or add null guards before assignment.",
                            confidence: Confidence.Medium));
                    }
                }
                assignmentCount = 0;
            }
        }
    }

    private void CheckUncheckedCasts(DiffLine line, List<Finding> findings)
    {
        foreach (var cast in UncheckedCastPatterns)
        {
            if (!line.Content.Contains(cast, StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                summary: $"Unchecked cast {cast} on potentially user-supplied value.",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Hard casts without overflow checking can cause silent data truncation or OverflowException.",
                suggestedAction: "Use checked{} blocks, Convert.ToInt32(), or int.TryParse() with validation.",
                confidence: Confidence.Medium));
            return;
        }
    }

    private void CheckSqlIgnore(DiffLine line, List<Finding> findings)
    {
        foreach (var pattern in SqlIgnorePatterns)
        {
            if (!line.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;

            findings.Add(CreateFinding(
                summary: $"SQL IGNORE/conflict-suppression pattern detected: {pattern}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Silently ignoring insert conflicts hides data integrity violations that should be investigated.",
                suggestedAction: "Handle conflicts explicitly with MERGE, UPSERT, or application-level logic.",
                confidence: Confidence.Medium));
            return;
        }
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA2227" or "CA1819"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0015",
                RuleName = "Data Integrity Risk",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a potential data integrity issue.",
                SuggestedAction = "Review the flagged property or collection for unintended mutability.",
                Confidence = Confidence.Medium,
            });
        }
    }
}
