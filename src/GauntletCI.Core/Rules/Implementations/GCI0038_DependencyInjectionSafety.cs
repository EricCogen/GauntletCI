// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0038 – Dependency Injection Safety
/// Detects DI anti-patterns: service locator usage, direct instantiation of injectable types,
/// and captive dependencies (singleton capturing scoped/transient services).
/// </summary>
public class GCI0038_DependencyInjectionSafety : RuleBase
{
    public override string Id => "GCI0038";
    public override string Name => "Dependency Injection Safety";

    private static readonly string[] ServiceLocatorPatterns =
    [
        "provider.GetService<",
        "provider.GetRequiredService<",
        "serviceProvider.GetService<",
        "serviceProvider.GetRequiredService<",
        "_serviceProvider.GetService<",
        "_serviceProvider.GetRequiredService<",
    ];

    private static readonly Regex DirectInstantiationRegex =
        new(@"new [A-Z][a-zA-Z]*(Service|Repository|Manager|Handler|Client)\(", RegexOptions.Compiled);

    private static readonly string[] DirectInstantiationExclusions = ["// ", "Mock<", "Fake<", "Stub<"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckServiceLocator(file, findings);
            CheckDirectInstantiation(file, findings);
            CheckCaptiveDependency(file, findings);
        }

        return Task.FromResult(findings);
    }

    private static bool IsInfrastructureFile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "Program.cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(fileName, "Startup.cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith("Extensions.cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("ServiceCollection", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("spec", StringComparison.OrdinalIgnoreCase);

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
                summary: "Service locator anti-pattern detected",
                evidence: $"{file.NewPath} line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Service locator hides dependencies, makes testing harder, and couples code to the DI container.",
                suggestedAction: "Inject the dependency directly via constructor injection instead of resolving it at runtime.",
                confidence: Confidence.High,
                line: line));
        }
    }

    private void CheckDirectInstantiation(DiffFile file, List<Finding> findings)
    {
        if (IsTestFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            if (DirectInstantiationExclusions.Any(e => line.Content.Contains(e, StringComparison.Ordinal)))
                continue;

            if (!DirectInstantiationRegex.IsMatch(line.Content)) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Direct instantiation of injectable type",
                evidence: $"{file.NewPath} line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Directly instantiating services bypasses the DI container, making the dependency untestable and unswappable.",
                suggestedAction: "Register the type with the DI container and inject it via constructor.",
                confidence: Confidence.Low,
                line: line));
        }
    }

    private void CheckCaptiveDependency(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        bool hasSingleton = addedLines.Any(l =>
            l.Content.Contains("AddSingleton<", StringComparison.Ordinal));
        bool hasScopedOrTransient = addedLines.Any(l =>
            l.Content.Contains("AddScoped<", StringComparison.Ordinal) ||
            l.Content.Contains("AddTransient<", StringComparison.Ordinal));

        if (!hasSingleton || !hasScopedOrTransient) return;

        var firstEvidence = addedLines
            .Where(l =>
                l.Content.Contains("AddSingleton<", StringComparison.Ordinal) ||
                l.Content.Contains("AddScoped<", StringComparison.Ordinal) ||
                l.Content.Contains("AddTransient<", StringComparison.Ordinal))
            .Select(l => l.Content.Trim())
            .FirstOrDefault() ?? string.Empty;

        findings.Add(CreateFinding(
            file,
            summary: "Potential captive dependency: singleton may capture scoped service",
            evidence: $"{file.NewPath}: mixed lifetimes detected — {firstEvidence}",
            whyItMatters: "A singleton that depends on a scoped service will capture a stale instance, causing bugs that are hard to diagnose.",
            suggestedAction: "Ensure singleton services only depend on other singletons, or use IServiceScopeFactory to create scopes explicitly.",
            confidence: Confidence.Medium));
    }
}
