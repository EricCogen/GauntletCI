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
    private static readonly string[] HttpContextSignals =
    [
        "Request.Form", "Request.Query", "Request.Body",
        "HttpContext.Request", "[FromBody]", "[FromForm]", "[FromQuery]"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;
            CheckMassAssignment(file, findings);
            CheckUnsafeHttpInputBinding(file, findings);
        }

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;
            CheckUncheckedCastsInFile(file, findings);
            foreach (var line in file.AddedLines)
            {
                CheckSqlIgnore(line, findings);
            }
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckUnsafeHttpInputBinding(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        bool hasHttpSignal = addedLines.Any(l =>
            HttpContextSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)));

        if (!hasHttpSignal) return;

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
                    findings.Add(CreateFinding(
                        file,
                        summary: "Possible unsafe HTTP input binding — mass-assignment without allowlist",
                        evidence: $"Starting at line {firstLine} in {file.NewPath}",
                        whyItMatters: "Binding HTTP request data directly to entity properties without an explicit allowlist (e.g. [Bind], DTO projection, or manual mapping) can expose internal fields to over-posting attacks (OWASP A03).",
                        suggestedAction: "Use a dedicated DTO or add [Bind(Include=...)] to restrict which properties can be set from request data.",
                        confidence: Confidence.High));
                }
                assignmentCount = 0;
            }
        }
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
                            file,
                            summary: $"Mass field assignment ({assignmentCount} assignments) without null validation in {file.NewPath}.",
                            evidence: $"Starting at line {firstLine} in {file.NewPath}",
                            whyItMatters: "Direct field assignment from user input without validation can lead to data corruption or over-posting attacks.",
                            suggestedAction: "Validate input with a DTO/ViewModel, use FluentValidation, or add null guards before assignment.",
                            confidence: Confidence.Low));
                    }
                }
                assignmentCount = 0;
            }
        }
    }

    private void CheckUncheckedCastsInFile(DiffFile file, List<Finding> findings)
    {
        // Only flag unchecked numeric casts when HTTP input signals are present in the file.
        // A cast like (int)Request.Form["id"] is dangerous; (int)someInternalCounter is not.
        bool hasHttpSignal = file.AddedLines.Any(l =>
            HttpContextSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)));

        if (!hasHttpSignal) return;

        foreach (var line in file.AddedLines)
        {
            foreach (var cast in UncheckedCastPatterns)
            {
                if (!line.Content.Contains(cast, StringComparison.Ordinal)) continue;

                findings.Add(CreateFinding(
                    summary: $"Unchecked cast {cast} on potentially user-supplied value.",
                    evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters: "Hard casts without overflow checking can cause silent data truncation or OverflowException.",
                    suggestedAction: "Use checked{} blocks, Convert.ToInt32(), or int.TryParse() with validation.",
                    confidence: Confidence.Low));
                break;
            }
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
