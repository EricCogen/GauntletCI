// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0030 – IDisposable Resource Safety
/// Detects disposable resources allocated without a using statement, using suffix-based type detection.
/// </summary>
public class GCI0030_DisposableResourceSafety : RuleBase
{
    public override string Id => "GCI0030";
    public override string Name => "IDisposable Resource Safety";

    private static readonly Regex TypeRegex =
        new(@"new ([A-Z][A-Za-z0-9]+)\(", RegexOptions.Compiled);

    private static readonly string[] DisposableSuffixes =
    [
        "Stream", "Reader", "Writer", "Connection", "Command", "Client",
        "Listener", "Channel", "Context", "Provider", "Session", "Transaction",
        "Certificate", "Scope", "Timer", "Enumerator"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            CheckUnguardedDisposables(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckUnguardedDisposables(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content;

            var match = TypeRegex.Match(content);
            if (!match.Success) continue;

            var typeName = match.Groups[1].Value;
            if (!DisposableSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal)))
                continue;

            if (content.Contains("using ", StringComparison.Ordinal)) continue;

            bool prevHasUsing = false;
            for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
            {
                var prev = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(prev)) continue;
                if (prev.StartsWith("using ") || prev.StartsWith("await using "))
                { prevHasUsing = true; break; }
                break;
            }
            if (prevHasUsing) continue;

            int winStart = Math.Max(0, i - 2);
            int winEnd = Math.Min(allLines.Count, i + 20);
            bool hasDispose = allLines[winStart..winEnd].Any(l =>
                l.Content.Contains(".Dispose()", StringComparison.Ordinal) ||
                l.Content.Contains("finally", StringComparison.Ordinal));
            if (hasDispose) continue;

            findings.Add(CreateFinding(
                summary: $"{typeName} allocated without using statement in {file.NewPath}.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: $"{typeName} likely implements IDisposable. Without using, it leaks OS handles or connection pool slots under exceptions.",
                suggestedAction: $"Wrap in `using var resource = new {typeName}(...);` to guarantee disposal.",
                confidence: Confidence.High));
        }
    }
}
