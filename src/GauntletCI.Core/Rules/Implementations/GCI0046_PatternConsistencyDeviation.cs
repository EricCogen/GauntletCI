// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0046, Pattern Consistency Deviation
/// Detects service locator anti-patterns and mixed sync/async naming within the same file.
/// </summary>
public class GCI0046_PatternConsistencyDeviation : RuleBase, IConfigurableRule
{
    public GCI0046_PatternConsistencyDeviation(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0046";
    public override string Name => "Pattern Consistency Deviation";

    private static readonly string[] ServiceLocatorPatterns =
        [
            ".GetService<",      // IServiceProvider.GetService<T>()
            ".GetRequiredService<",  // IServiceProvider.GetRequiredService<T>()
            "ServiceLocator.Current",  // Legacy ServiceLocator
            "ServiceLocator.GetService",  // ServiceLocator static method
            "GetService(",  // Bare GetService call (legacy ASMX, ObjectFactory)
            ".Resolve<",  // Autofac-style DI
            ".GetInstance<",  // Autofac/CastleWindsor-style DI
            "container.Resolve",  // Container.Resolve pattern
        ];

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

    // Returns true when the pattern appears inside a string literal on this line
    // (i.e., an odd number of unescaped quotes precede the match position).
    private static bool IsInsideStringLiteral(string content, string pattern)
    {
        int idx = content.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return false;
        int quotes = 0;
        for (int i = 0; i < idx; i++)
        {
            if (content[i] == '"' && (i == 0 || content[i - 1] != '\\'))
                quotes++;
        }
        return quotes % 2 == 1;
    }

    private static readonly HashSet<string> FrameworkExemptPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard BCL async interface pairs: adding both sync+async is required by .NET design
        "Dispose", "Flush", "Open", "Close", "Connect", "Disconnect",
        "Read", "Write", "Serialize", "Deserialize", "Initialize",
        "Shutdown", "Start", "Stop", "Subscribe", "Unsubscribe",
        "Publish", "Send", "Receive", "Execute", "Invoke", "Run",
        "GetUniverseDomain", "GetAccessToken",
    };

    private HashSet<string> _allowedSyncAsyncPairs = new(StringComparer.Ordinal);

    public void Configure(GauntletConfig config)
    {
        _allowedSyncAsyncPairs = new HashSet<string>(
            config.PatternConsistency?.AllowedSyncAsyncPairs ?? [],
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

        // Check both added and context lines to catch service locator patterns
        // that may appear in modified sections or existing code
        var allAddedOrContextLines = file.Hunks
            .SelectMany(h => h.Lines)
            .Where(l => l.Kind == DiffLineKind.Added || l.Kind == DiffLineKind.Context)
            .ToList();

        foreach (var line in allAddedOrContextLines)
        {
            var matched = ServiceLocatorPatterns.FirstOrDefault(
                p => line.Content.Contains(p, StringComparison.Ordinal));

            if (matched is null) continue;

            // Skip pattern-definition arrays: the match is inside a string literal
            if (IsInsideStringLiteral(line.Content, matched)) continue;

            // Only flag added lines (not context) to avoid over-reporting
            if (line.Kind != DiffLineKind.Added) continue;

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
        // Get all added method names
        var addedMethodNames = file.AddedLines
            .Select(l => MethodNameRegex.Match(l.Content))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Also check context lines to catch existing methods paired with new ones
        var allMethodLines = file.Hunks
            .SelectMany(h => h.Lines)
            .Where(l => l.Kind == DiffLineKind.Added || l.Kind == DiffLineKind.Context);

        var allMethodNames = allMethodLines
            .Select(l => MethodNameRegex.Match(l.Content))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Flag if ANY added method creates a sync+async pair with existing methods
        var asyncMethodBases = addedMethodNames
            .Where(n => n.EndsWith("Async", StringComparison.Ordinal))
            .Select(n => n[..^"Async".Length])
            .ToList();

        foreach (var baseName in asyncMethodBases)
        {
            // Check if the sync variant exists (either added or in context)
            if (!allMethodNames.Contains(baseName)) continue;

            // Skip pairs in the framework-exempt list (standard .NET async interface pairs)
            if (FrameworkExemptPairs.Contains(baseName)) continue;

            // Skip pairs that are intentionally sync+async (configured allowlist)
            if (_allowedSyncAsyncPairs.Contains(baseName)) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Mixed sync/async: both '{baseName}' and '{baseName}Async' in same file",
                evidence: $"{Path.GetFileName(file.NewPath)}: adds {baseName}Async() with existing/added {baseName}()",
                whyItMatters: "Exposing sync and async variants with the same base name creates confusion about which to call, risks accidental deadlock, and violates the async-all-the-way principle.",
                suggestedAction: "Provide only the async variant and let callers use .GetAwaiter().GetResult() if blocking is truly needed, or adopt the async-all-the-way pattern throughout.",
                confidence: Confidence.Low));
        }

        // Also check reverse: if sync methods are added, flag async variants
        var syncMethodBases = addedMethodNames
            .Where(n => !n.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        foreach (var baseName in syncMethodBases)
        {
            var asyncName = baseName + "Async";
            if (!allMethodNames.Contains(asyncName)) continue;

            // Skip framework-exempt and configured pairs
            if (FrameworkExemptPairs.Contains(baseName)) continue;
            if (_allowedSyncAsyncPairs.Contains(baseName)) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Mixed sync/async: both '{baseName}' and '{baseName}Async' in same file",
                evidence: $"{Path.GetFileName(file.NewPath)}: adds {baseName}() with existing/added {asyncName}()",
                whyItMatters: "Exposing sync and async variants with the same base name creates confusion about which to call, risks accidental deadlock, and violates the async-all-the-way principle.",
                suggestedAction: "Provide only the async variant and let callers use .GetAwaiter().GetResult() if blocking is truly needed, or adopt the async-all-the-way pattern throughout.",
                confidence: Confidence.Low));
        }
    }
}
