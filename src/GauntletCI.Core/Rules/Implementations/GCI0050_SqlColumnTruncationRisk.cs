// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0050 – SQL Column Truncation Risk
/// Detects short string column definitions (<c>nvarchar(N)</c>, <c>varchar(N)</c>,
/// <c>[StringLength(N)]</c>, <c>[MaxLength(N)]</c>, or <c>HasMaxLength(N)</c>)
/// where N &lt; 100, in EF migration and model files (<c>.cs</c>).
/// Short column widths silently truncate user-supplied strings at the database layer,
/// causing data loss without an exception.
/// </summary>
public class GCI0050_SqlColumnTruncationRisk : RuleBase
{
    public override string Id   => "GCI0050";
    public override string Name => "SQL Column Truncation Risk";

    // nvarchar(N) or varchar(N) with N captured
    private static readonly Regex VarcharRegex = new(
        @"\bn?varchar\s*\(\s*(\d+)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // [StringLength(N)] or [MaxLength(N)] EF / DataAnnotations attributes
    // Captures through the closing )] so match.Value shows the full attribute in findings
    private static readonly Regex StringLengthAttributeRegex = new(
        @"\[(?:StringLength|MaxLength)\s*\(\s*(\d+)[^)]*\)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // HasMaxLength(N) EF Fluent API
    private static readonly Regex HasMaxLengthRegex = new(
        @"\bHasMaxLength\s*\(\s*(\d+)\s*\)", RegexOptions.Compiled);

    private const int TruncationThreshold = 100;

    // EF/ADO.NET column definitions live in C# migration and configuration files
    private static readonly string[] TargetExtensions = [".cs"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            var ext = Path.GetExtension(file.NewPath).ToLowerInvariant();
            if (!TargetExtensions.Contains(ext)) continue;

            // Only fire if the file looks like a migration, schema, or EF model
            if (!IsMigrationOrSchemaFile(file.NewPath)) continue;

            foreach (var line in file.AddedLines)
            {
                var trimmed = line.Content.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("--") || trimmed.StartsWith("*"))
                    continue;

                if (TryGetShortLength(line.Content, out int length, out string pattern))
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Short string column ({pattern}) may silently truncate user input",
                        evidence: $"Line {line.LineNumber}: {(trimmed.Length > 120 ? trimmed[..120] + "…" : trimmed)}",
                        whyItMatters: $"A column width of {length} characters will silently drop any input longer than {length} chars " +
                                      "at the database layer. If users can provide this value, data loss occurs without an exception — " +
                                      "the application continues without any error signal.",
                        suggestedAction: $"Increase the column width (e.g. nvarchar(256) or nvarchar(max)) or add server-side validation " +
                                         $"that rejects strings longer than {length} characters before they reach the database.",
                        confidence: Confidence.Medium,
                        line: line));
                }
            }
        }

        return Task.FromResult(findings);
    }

    private static bool TryGetShortLength(string content, out int length, out string pattern)
    {
        foreach (var regex in new[] { VarcharRegex, StringLengthAttributeRegex, HasMaxLengthRegex })
        {
            var match = regex.Match(content);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int n) && n < TruncationThreshold)
            {
                length  = n;
                pattern = match.Value.Trim();
                return true;
            }
        }

        length  = 0;
        pattern = string.Empty;
        return false;
    }

    /// <summary>
    /// Returns true when the file path suggests it contains database schema or migration definitions.
    /// Targets EF migrations, DbContext / entity model, and fluent configuration files (.cs only).
    /// </summary>
    private static bool IsMigrationOrSchemaFile(string path)
    {
        var lower = path.Replace('\\', '/').ToLowerInvariant();
        // Use "/migration" (with leading slash) to avoid matching "ImmigrationService.cs"
        return lower.Contains("/migration")
            || lower.Contains("migration.cs")  // filename: "UserMigration.cs", "AddUsersMigration.cs"
            || lower.Contains("schema")
            || lower.Contains("dbcontext")
            || lower.Contains("entityconfig")
            || lower.Contains("modelbuilder")
            || lower.Contains("fluent");
    }
}
