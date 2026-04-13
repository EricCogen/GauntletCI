// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0014 – Rollback Safety
/// Detects irreversible operations: DDL, file deletion, migration without Down().
/// </summary>
public class GCI0014_RollbackSafety : RuleBase
{
    public override string Id => "GCI0014";
    public override string Name => "Rollback Safety";

    private static readonly string[] DdlKeywords =
        ["DROP TABLE", "ALTER TABLE", "DROP COLUMN", "TRUNCATE", "DROP DATABASE", "DROP INDEX"];

    private static readonly string[] DeletionApis =
        ["File.Delete(", "Directory.Delete(", "Environment.Exit(", "Application.Exit("];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var line in diff.AllAddedLines)
        {
            CheckDdl(line, findings);
            CheckDeletionApis(line, findings);
        }

        CheckMigrationWithoutDown(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckDdl(DiffLine line, List<Finding> findings)
    {
        foreach (var keyword in DdlKeywords)
        {
            if (!line.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            findings.Add(CreateFinding(
                summary: $"Irreversible DDL statement detected: {keyword}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "DDL operations like DROP and TRUNCATE are destructive and cannot be rolled back in most databases.",
                suggestedAction: "Ensure a backup exists, test in staging first, and add a compensating migration Down() method.",
                confidence: Confidence.High));
            return;
        }
    }

    private void CheckDeletionApis(DiffLine line, List<Finding> findings)
    {
        foreach (var api in DeletionApis)
        {
            if (!line.Content.Contains(api, StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                summary: $"Destructive API call: {api}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "File deletion and process exit are hard to reverse and may cause data loss.",
                suggestedAction: "Add confirmation, use soft-delete patterns, or ensure the operation is recoverable.",
                confidence: Confidence.Medium));
            return;
        }
    }

    private void CheckMigrationWithoutDown(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (!file.NewPath.Contains("Migration", StringComparison.OrdinalIgnoreCase) &&
                !file.NewPath.Contains("migration", StringComparison.Ordinal)) continue;

            var allAddedContent = string.Join("\n", file.AddedLines.Select(l => l.Content));
            bool hasUp = allAddedContent.Contains("void Up(", StringComparison.Ordinal) ||
                         allAddedContent.Contains("Up(MigrationBuilder", StringComparison.Ordinal);
            bool hasDown = allAddedContent.Contains("void Down(", StringComparison.Ordinal) ||
                           allAddedContent.Contains("Down(MigrationBuilder", StringComparison.Ordinal);

            if (hasUp && !hasDown)
            {
                findings.Add(CreateFinding(
                    summary: $"Database migration has Up() but no Down() method in {file.NewPath}.",
                    evidence: $"File: {file.NewPath} — Up() found, Down() not found in added lines.",
                    whyItMatters: "Migrations without a Down() method cannot be rolled back, making recovery from bad deployments impossible.",
                    suggestedAction: "Implement the Down() method to make the migration reversible.",
                    confidence: Confidence.High));
            }
        }
    }
}
