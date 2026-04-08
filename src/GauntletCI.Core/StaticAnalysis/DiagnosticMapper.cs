// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Maps Roslyn/CA diagnostic IDs to GCI rule findings.
/// </summary>
public static class DiagnosticMapper
{
    private static readonly Dictionary<string, (string RuleId, string RuleName, Confidence Confidence)> Mappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // SQL injection
            ["CA2100"] = ("GCI0012", "Security Risk", Confidence.High),
            ["CA2101"] = ("GCI0012", "Security Risk", Confidence.High),
            ["CA2153"] = ("GCI0012", "Security Risk", Confidence.High),
            // Exception handling
            ["CA1031"] = ("GCI0007", "Error Handling Integrity", Confidence.High),
            ["CA2000"] = ("GCI0007", "Error Handling Integrity", Confidence.Medium),
            ["CA1001"] = ("GCI0007", "Error Handling Integrity", Confidence.Medium),
            // Method complexity
            ["CA1822"] = ("GCI0008", "Complexity Control", Confidence.Low),
            // Argument validation
            ["CA1062"] = ("GCI0006", "Edge Case Handling", Confidence.Medium),
            // String comparison
            ["CA1305"] = ("GCI0009", "Consistency with Patterns", Confidence.Low),
            ["CA1307"] = ("GCI0009", "Consistency with Patterns", Confidence.Low),
            ["CA1309"] = ("GCI0009", "Consistency with Patterns", Confidence.Low),
            ["CA1711"] = ("GCI0009", "Consistency with Patterns", Confidence.Low),
            ["CA1720"] = ("GCI0009", "Consistency with Patterns", Confidence.Low),
            // Data integrity
            ["CA2227"] = ("GCI0015", "Data Integrity Risk", Confidence.Medium),
            ["CA1819"] = ("GCI0015", "Data Integrity Risk", Confidence.Medium),
        };

    /// <summary>
    /// Maps diagnostics from an <see cref="AnalyzerResult"/> to <see cref="Finding"/> objects.
    /// </summary>
    public static IEnumerable<Finding> MapDiagnostics(AnalyzerResult result)
    {
        if (!result.Success) yield break;

        foreach (var diag in result.Diagnostics)
        {
            if (!Mappings.TryGetValue(diag.Id, out var mapping)) continue;

            yield return new Finding
            {
                RuleId = mapping.RuleId,
                RuleName = mapping.RuleName,
                Summary = $"Roslyn diagnostic {diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line},{diag.Column}",
                WhyItMatters = $"Static analysis flagged {diag.Id} — a known code quality or security concern.",
                SuggestedAction = "Review the diagnostic and address the underlying issue.",
                Confidence = mapping.Confidence,
            };
        }
    }
}
