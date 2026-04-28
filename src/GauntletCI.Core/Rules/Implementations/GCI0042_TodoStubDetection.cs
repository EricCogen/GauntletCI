// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0042, TODO/Stub Detection
/// Fires when added lines in non-test files contain TODO, FIXME, HACK, or throw new NotImplementedException.
/// </summary>
public class GCI0042_TodoStubDetection : RuleBase
{
    public override string Id => "GCI0042";
    public override string Name => "TODO/Stub Detection";

    private static readonly string[] StubKeywords = ["TODO", "FIXME", "HACK"];

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("spec", StringComparison.OrdinalIgnoreCase);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files.Where(f => !IsTestFile(f.NewPath)))
        {
            var evidence = new List<string>();

            foreach (var line in file.AddedLines)
            {
                var content = line.Content;
                var trimmed = content.TrimStart();

                // XML doc comments are meta-documentation, not production stubs
                if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;

                bool isLineComment = trimmed.StartsWith("//", StringComparison.Ordinal);
                if (isLineComment)
                {
                    // For comment lines, require the marker to be the first token after //
                    // This prevents "hvc1 hack variant" or similar prose matches
                    var commentBody = trimmed[2..].TrimStart();
                    if (StubKeywords.Any(k => commentBody.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
                        evidence.Add($"Line {line.LineNumber}: {trimmed}");
                }
                else if (StubKeywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    evidence.Add($"Line {line.LineNumber}: {trimmed}");
                else if (content.Contains("throw new NotImplementedException", StringComparison.Ordinal))
                    evidence.Add($"Line {line.LineNumber}: {trimmed}");
            }

            if (evidence.Count == 0) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"{evidence.Count} TODO/stub pattern(s) found in {Path.GetFileName(file.NewPath)}",
                evidence: string.Join("; ", evidence.Take(5)),
                whyItMatters: "TODO, FIXME, HACK markers and NotImplementedException stubs indicate incomplete code that can crash or misbehave in production.",
                suggestedAction: "Resolve all TODO/FIXME/HACK comments and replace NotImplementedException stubs with real implementations before merging.",
                confidence: Confidence.Medium));
        }

        return Task.FromResult(findings);
    }
}
