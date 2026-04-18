// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0046 – Pattern Consistency Deviation
/// Detects service locator anti-patterns and mixed sync/async naming within the same file.
/// </summary>
public class GCI0046_PatternConsistencyDeviation : RuleBase, IConfigurableRule
{
    public override string Id => "GCI0046";
    public override string Name => "Pattern Consistency Deviation";

    private static readonly string[] ServiceLocatorPatterns =
        [".GetService<", ".GetRequiredService<", "ServiceLocator.Current"];

    private static readonly Regex MethodNameRegex =
        new(@"(?:public|private|protected|internal)\s+(?:async\s+)?(?:Task|void|[\w<>\[\]]+)\s+(\w+)\s*\(",
            RegexOptions.Compiled);

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("spec", StringComparison.OrdinalIgnoreCase);

    private static bool IsInfrastructureFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.Equals(fileName, "Program.cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Startup.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Extensions.cs", StringComparison.OrdinalIgnoreCase);
    }

    private HashSet<string> _allowedSyncAsyncPairs = new(StringComparer.Ordinal);

    public void Configure(GauntletConfig config)
    {
        _allowedSyncAsyncPairs = new HashSet<string>(
            config.PatternConsistency.AllowedSyncAsyncPairs,
            StringComparer.Ordinal);
    }

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files.Where(f => !IsTestFile(f.NewPath)))
        {
            CheckServiceLocator(file, findings);
            CheckMixedSyncAsync(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckServiceLocator(DiffFile file, List<Finding> findings)
    {
        if (IsInfrastructureFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var matched = ServiceLocatorPatterns.FirstOrDefault(
                p => line.Content.Contains(p, StringComparison.Ordinal));

            if (matched is null) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Service locator anti-pattern deviates from constructor injection convention",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Mixing service locator calls with constructor injection creates inconsistency, hides dependencies, and makes the code harder to test.",
                suggestedAction: "Inject the dependency via the constructor to maintain consistency with the rest of the codebase.",
                confidence: Confidence.Low,
                line: line));
        }
    }

    private void CheckMixedSyncAsync(DiffFile file, List<Finding> findings)
    {
        var addedMethodNames = file.AddedLines
            .Select(l => MethodNameRegex.Match(l.Content))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var asyncMethodBases = addedMethodNames
            .Where(n => n.EndsWith("Async", StringComparison.Ordinal))
            .Select(n => n[..^"Async".Length])
            .ToList();

        foreach (var baseName in asyncMethodBases)
        {
            if (!addedMethodNames.Contains(baseName)) continue;

            // Skip pairs that are intentionally sync+async (configured allowlist)
            if (_allowedSyncAsyncPairs.Contains(baseName)) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Mixed sync/async: both '{baseName}' and '{baseName}Async' added in same file",
                evidence: $"{Path.GetFileName(file.NewPath)}: adds both {baseName}() and {baseName}Async()",
                whyItMatters: "Exposing sync and async variants with the same base name creates confusion about which to call, risks accidental deadlock, and violates the async-all-the-way principle.",
                suggestedAction: "Provide only the async variant and let callers use .GetAwaiter().GetResult() if blocking is truly needed, or adopt the async-all-the-way pattern throughout.",
                confidence: Confidence.Low));
        }
    }
}
