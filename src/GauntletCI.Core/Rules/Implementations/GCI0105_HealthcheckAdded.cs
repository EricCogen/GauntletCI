// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0105 – Healthcheck Added Without Documentation
/// Fires when a HEALTHCHECK directive is added with no accompanying comment line above it in the diff.
/// Undocumented healthchecks are hard to tune when containers are marked unhealthy.
/// </summary>
public class GCI0105_HealthcheckAdded : RuleBase
{
    public override string Id   => "GCI0105";
    public override string Name => "Healthcheck Added Without Documentation";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllDiffFiles)
        {
            if (!IsDockerfile(file.NewPath)) continue;

            var allAddedLines = file.AddedLines.ToList();

            for (int i = 0; i < allAddedLines.Count; i++)
            {
                var line = allAddedLines[i];
                if (!line.Content.TrimStart().StartsWith("HEALTHCHECK ", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Look for a comment on the immediately preceding added line
                bool hasComment = i > 0
                    && allAddedLines[i - 1].Content.TrimStart().StartsWith('#');

                if (!hasComment)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"HEALTHCHECK added without documentation in {file.NewPath}",
                        evidence: line.Content.Trim(),
                        whyItMatters: "Healthchecks that fail cause containers to be marked unhealthy and restarted. Undocumented healthchecks are hard to tune and diagnose.",
                        suggestedAction: "Add a comment above the HEALTHCHECK directive explaining the endpoint, expected interval, timeout, and failure thresholds.",
                        confidence: Confidence.Medium,
                        line: line));
                }
            }
        }

        return Task.FromResult(findings);
    }

    private static bool IsDockerfile(string? path)
    {
        if (path is null) return false;
        var name = Path.GetFileName(path);
        return name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dockerfile", StringComparison.OrdinalIgnoreCase)
            || path.Contains("dockerfile", StringComparison.OrdinalIgnoreCase);
    }
}
