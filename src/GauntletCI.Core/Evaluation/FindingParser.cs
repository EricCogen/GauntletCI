// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class FindingParser
{
    private static readonly Regex EvidenceRegex = new(@"([A-Za-z0-9_\-/]+\.[A-Za-z0-9]+(:\d+)?|[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    public IReadOnlyList<Finding> Parse(string rawJson)
    {
        try
        {
            List<Finding>? findings = JsonSerializer.Deserialize<List<Finding>>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (findings is null)
            {
                return [];
            }

            List<Finding> filtered = [];
            foreach (Finding finding in findings)
            {
                bool hasRequiredFields =
                    !string.IsNullOrWhiteSpace(finding.RuleId) &&
                    !string.IsNullOrWhiteSpace(finding.RuleName) &&
                    !string.IsNullOrWhiteSpace(finding.Severity) &&
                    !string.IsNullOrWhiteSpace(finding.FindingText) &&
                    !string.IsNullOrWhiteSpace(finding.Evidence);

                if (!hasRequiredFields || IsVague(finding))
                {
                    continue;
                }

                filtered.Add(finding);
            }

            return filtered;
        }
        catch (JsonException ex)
        {
            throw new FormatException("Model response was not valid finding JSON.", ex);
        }
    }

    private static bool IsVague(Finding finding)
    {
        if (!EvidenceRegex.IsMatch(finding.Evidence))
        {
            return true;
        }

        string lowered = finding.FindingText.Trim().ToLowerInvariant();
        return lowered.Contains("may need review", StringComparison.Ordinal) ||
               lowered.Contains("consider reviewing", StringComparison.Ordinal) ||
               lowered.Length < 20;
    }
}
