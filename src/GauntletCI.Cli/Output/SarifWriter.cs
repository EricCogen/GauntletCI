// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Serialises an <see cref="EvaluationResult"/> to SARIF 2.1.0 format so findings can be
/// consumed by GitHub Advanced Security, DefectDojo, and other SARIF-compatible tools.
/// </summary>
public static class SarifWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes a SARIF 2.1.0 document to <see cref="Console.Out"/>.</summary>
    public static void Write(EvaluationResult result)
        => Console.WriteLine(Serialize(result));

    /// <summary>Builds and returns the SARIF 2.1.0 JSON string.</summary>
    public static string Serialize(EvaluationResult result)
    {
        var rules = result.Findings
            .Select(f => new { f.RuleId, f.RuleName })
            .DistinctBy(r => r.RuleId)
            .OrderBy(r => r.RuleId)
            .Select(r => (object)new
            {
                id = r.RuleId,
                name = SanitizeName(r.RuleName),
                shortDescription = new { text = r.RuleName },
                helpUri = $"https://gauntletci.com/rules/{r.RuleId}",
            })
            .ToList();

        var results = result.Findings.Select(f => BuildResult(f)).ToList();

        var sarif = new
        {
            @__schema = "https://json.schemastore.org/sarif-2.1.0.json",
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "GauntletCI",
                            version = "2.0.0",
                            informationUri = "https://gauntletci.com",
                            rules,
                        }
                    },
                    results,
                }
            }
        };

        // Rename __schema → $schema after serialization (JsonSerializer can't emit $ keys)
        var json = JsonSerializer.Serialize(sarif, JsonOptions);
        return json.Replace("\"__schema\"", "\"$schema\"");
    }

    private static object BuildResult(Finding finding)
    {
        var level = finding.Severity switch
        {
            RuleSeverity.Block    => "error",
            RuleSeverity.Warn     => "warning",
            _                     => "note",
        };

        var message = string.IsNullOrWhiteSpace(finding.WhyItMatters)
            ? finding.Summary
            : $"{finding.Summary}: {finding.WhyItMatters}";

        if (!string.IsNullOrWhiteSpace(finding.SuggestedAction))
            message += $" Action: {finding.SuggestedAction}";

        if (finding.FilePath is { } path && finding.Line is { } line)
        {
            return new
            {
                ruleId = finding.RuleId,
                message = new { text = message },
                level,
                locations = new[]
                {
                    new
                    {
                        physicalLocation = new
                        {
                            artifactLocation = new
                            {
                                uri = path.Replace('\\', '/'),
                                uriBaseId = "%SRCROOT%",
                            },
                            region = new { startLine = line },
                        }
                    }
                },
            };
        }

        return new
        {
            ruleId = finding.RuleId,
            message = new { text = message },
            level,
        };
    }

    private static string SanitizeName(string name) =>
        new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());
}
