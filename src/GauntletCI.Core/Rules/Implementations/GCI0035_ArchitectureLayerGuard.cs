// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0035 – Architecture Layer Guard
/// Checks added using directives against configured forbidden import pairs.
/// </summary>
public class GCI0035_ArchitectureLayerGuard : RuleBase, IConfigurableRule
{
    public override string Id => "GCI0035";
    public override string Name => "Architecture Layer Guard";

    private static readonly Regex UsingRegex =
        new(@"^\s*using\s+([\w.]+)\s*;", RegexOptions.Compiled);

    private Dictionary<string, List<string>> _forbiddenImports = new();

    public void Configure(GauntletConfig config)
    {
        _forbiddenImports = config.ForbiddenImports ?? new();
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        if (_forbiddenImports.Count == 0) return Task.FromResult(findings);

        foreach (var file in diff.Files)
        {
            foreach (var line in file.AddedLines)
            {
                var match = UsingRegex.Match(line.Content);
                if (!match.Success) continue;

                var importedNs = match.Groups[1].Value;

                foreach (var (layer, forbidden) in _forbiddenImports)
                {
                    if (!file.NewPath.Contains(layer, StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (var forbiddenFragment in forbidden)
                    {
                        if (!importedNs.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase)) continue;

                        findings.Add(CreateFinding(
                            summary: $"Forbidden import '{importedNs}' in {file.NewPath}: layer '{layer}' must not depend on '{forbiddenFragment}'.",
                            evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                            whyItMatters: "Cross-layer dependencies break architectural boundaries, increase coupling, and make the codebase harder to test and maintain.",
                            suggestedAction: "Move the dependency to a more appropriate layer, or introduce an abstraction (interface/adapter) to invert the dependency.",
                            confidence: Confidence.High));
                    }
                }
            }
        }

        return Task.FromResult(findings);
    }
}
