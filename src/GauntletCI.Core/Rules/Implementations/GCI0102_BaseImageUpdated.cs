// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0102 – Base Image Updated
/// Detects when a FROM directive is changed in a Dockerfile.
/// Base image updates can introduce breaking changes in system libs, env vars, or default users.
/// </summary>
public class GCI0102_BaseImageUpdated : RuleBase
{
    public override string Id   => "GCI0102";
    public override string Name => "Base Image Updated";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllDiffFiles)
        {
            if (!IsDockerfile(file.NewPath)) continue;

            var removedFrom = file.RemovedLines
                .Where(l => l.Content.TrimStart().StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var addedFrom = file.AddedLines
                .Where(l => l.Content.TrimStart().StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removedFrom.Count == 0 || addedFrom.Count == 0) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Base image updated in {file.NewPath}",
                evidence: $"Was: {removedFrom[0].Content.Trim()} | Now: {addedFrom[0].Content.Trim()}",
                whyItMatters: "Base image updates can introduce breaking changes in system libraries, environment variables, or default users.",
                suggestedAction: "Run full integration tests with the new base image. Check the image changelog for breaking changes.",
                confidence: Confidence.High,
                line: addedFrom[0]));
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
