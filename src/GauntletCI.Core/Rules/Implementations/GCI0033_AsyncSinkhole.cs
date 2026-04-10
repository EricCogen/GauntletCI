// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0033 – Async Sinkhole
/// Detects .Result property access or .Wait() calls that block threads and cause deadlocks.
/// </summary>
public class GCI0033_AsyncSinkhole : RuleBase
{
    public override string Id => "GCI0033";
    public override string Name => "Async Sinkhole";

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var line in file.AddedLines)
            {
                var content = line.Content;
                var trimmed = content.TrimStart();

                // Skip comments
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                if (content.Contains(".Result", StringComparison.Ordinal))
                {
                    findings.Add(CreateFinding(
                        summary: $"Blocking .Result access on a Task in {file.NewPath} — risk of deadlock.",
                        evidence: $"Line {line.LineNumber}: {trimmed}",
                        whyItMatters: "Calling .Result or .Wait() on a Task blocks the calling thread and can cause deadlocks in ASP.NET, WPF, or any context with a synchronization context.",
                        suggestedAction: "Use `await` instead of `.Result` or `.Wait()`. If calling from a sync context, use `Task.Run(() => ...).GetAwaiter().GetResult()` as a last resort.",
                        confidence: Confidence.High));
                    continue;
                }

                if ((content.Contains(".Wait()", StringComparison.Ordinal) ||
                     content.Contains(".Wait(", StringComparison.Ordinal)) &&
                    !content.Contains(".WaitOne(", StringComparison.Ordinal))
                {
                    findings.Add(CreateFinding(
                        summary: $"Blocking .Wait() call on a Task in {file.NewPath} — risk of deadlock.",
                        evidence: $"Line {line.LineNumber}: {trimmed}",
                        whyItMatters: "Calling .Result or .Wait() on a Task blocks the calling thread and can cause deadlocks in ASP.NET, WPF, or any context with a synchronization context.",
                        suggestedAction: "Use `await` instead of `.Result` or `.Wait()`. If calling from a sync context, use `Task.Run(() => ...).GetAwaiter().GetResult()` as a last resort.",
                        confidence: Confidence.High));
                }
            }
        }

        return Task.FromResult(findings);
    }
}
