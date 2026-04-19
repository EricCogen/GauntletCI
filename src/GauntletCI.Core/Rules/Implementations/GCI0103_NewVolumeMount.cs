// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0103 – New Volume Mount Added
/// Detects when a VOLUME directive is added to a Dockerfile.
/// New volumes change container data persistence behavior and may expose host paths.
/// </summary>
public class GCI0103_NewVolumeMount : RuleBase
{
    public override string Id   => "GCI0103";
    public override string Name => "New Volume Mount Added";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllDiffFiles)
        {
            if (!IsDockerfile(file.NewPath)) continue;

            foreach (var line in file.AddedLines)
            {
                if (!line.Content.TrimStart().StartsWith("VOLUME ", StringComparison.OrdinalIgnoreCase))
                    continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"New volume mount added in {file.NewPath}",
                    evidence: line.Content.Trim(),
                    whyItMatters: "New volumes change container data persistence behavior and may expose host paths to the container runtime.",
                    suggestedAction: "Verify the volume path is intentional, document what data is persisted, and confirm host-path exposure is acceptable.",
                    confidence: Confidence.High,
                    line: line));
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
