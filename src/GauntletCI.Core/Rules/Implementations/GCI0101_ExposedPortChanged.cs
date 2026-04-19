// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0101 – Exposed Port Changed
/// Detects when an EXPOSE directive is changed to a different port number in a Dockerfile.
/// Port changes break load balancer configs, firewall rules, and service discovery
/// without coordinated infra updates.
/// </summary>
public class GCI0101_ExposedPortChanged : RuleBase
{
    public override string Id   => "GCI0101";
    public override string Name => "Exposed Port Changed";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllDiffFiles)
        {
            if (!IsDockerfile(file.NewPath)) continue;

            var removedExpose = file.RemovedLines
                .Where(l => l.Content.TrimStart().StartsWith("EXPOSE ", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var addedExpose = file.AddedLines
                .Where(l => l.Content.TrimStart().StartsWith("EXPOSE ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removedExpose.Count == 0 || addedExpose.Count == 0) continue;

            var removedPort = ExtractPort(removedExpose[0].Content);
            var addedPort   = ExtractPort(addedExpose[0].Content);

            if (removedPort != null && addedPort != null && removedPort != addedPort)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Exposed port changed in {file.NewPath}",
                    evidence: $"Was: {removedExpose[0].Content.Trim()} | Now: {addedExpose[0].Content.Trim()}",
                    whyItMatters: "Port changes break load balancer configs, firewall rules, and service discovery without coordinated infra updates.",
                    suggestedAction: "Update all infra configs (load balancer, firewall rules, service mesh) atomically with this Dockerfile change.",
                    confidence: Confidence.High,
                    line: addedExpose[0]));
            }
        }

        return Task.FromResult(findings);
    }

    private static string? ExtractPort(string line) =>
        line.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts
            ? parts[1].Trim()
            : null;

    private static bool IsDockerfile(string? path)
    {
        if (path is null) return false;
        var name = Path.GetFileName(path);
        return name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dockerfile", StringComparison.OrdinalIgnoreCase)
            || path.Contains("dockerfile", StringComparison.OrdinalIgnoreCase);
    }
}
