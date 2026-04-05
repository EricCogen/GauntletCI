using System.Text.Json;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class FindingParser
{
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

                if (!hasRequiredFields)
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
}
