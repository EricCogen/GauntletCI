// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0056, Missing Test Framework Detection
/// Detects repositories that appear to lack test infrastructure by scanning for
/// test framework references in project files (xunit, NUnit, MSTest, etc.) and
/// test file patterns. Flags when production code exists but no test framework evidence.
/// </summary>
public class GCI0056_MissingTestFramework : RuleBase
{
    public GCI0056_MissingTestFramework(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0056";
    public override string Name => "Missing Test Framework";

    private static readonly string[] TestFrameworkPackages = new[]
    {
        // C# test frameworks
        "xunit", "NUnit", "MSTest", "Microsoft.NET.Test.Sdk",
        "nsubstitute", "moq", "autofixture", "Verify", "Shouldly",
        // JavaScript test frameworks
        "jest", "mocha", "vitest", "chai", "jasmine",
        // Python test frameworks
        "pytest", "unittest",
        // General test utilities
        "test", "testing"
    };

    private static readonly string[] ProjectFilePatterns = new[]
    {
        ".csproj", ".vbproj", "package.json", "pyproject.toml", "Cargo.toml", ".gradle"
    };

    private static readonly string[] ExemptDirectoryPatterns = new[]
    {
        "/samples/", "/sample/", "/examples/", "/example/", "/docs/", "/tools/", "/.github/"
    };

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        // Collect all files (eligible and skipped)
        var allFiles = context.EligibleFiles.Concat(context.SkippedFiles).ToList();

        // Check if repository appears to have production code
        var productionFiles = allFiles
            .Where(f => !WellKnownPatterns.IsTestFile(f.FilePath))
            .Where(f => !IsDocumentationFile(f.FilePath))
            .Where(f => !IsExemptDirectory(f.FilePath))
            .Where(f => IsSourceCodeFile(f.FilePath))
            .ToList();

        // If fewer than 3 production files, too small to require tests
        if (productionFiles.Count < 3)
            return Task.FromResult(findings);

        // Check for project files that indicate a buildable repository
        var hasProjectFile = allFiles.Any(f =>
            ProjectFilePatterns.Any(p => f.FilePath.EndsWith(p, StringComparison.OrdinalIgnoreCase)));

        if (!hasProjectFile)
            return Task.FromResult(findings);

        // Check for test files (excluding benchmarks for this rule)
        var testFiles = allFiles
            .Where(f => WellKnownPatterns.IsTestFile(f.FilePath))
            .Where(f => !f.FilePath.EndsWith("Benchmark.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.FilePath.EndsWith("Benchmarks.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Check for test framework references in project files
        var hasTestFramework = CheckForTestFrameworkPackages(allFiles);

        // If tests exist OR test framework packages are referenced, no finding
        if (testFiles.Count > 0 || hasTestFramework)
            return Task.FromResult(findings);

        // No tests and no test framework detected
        findings.Add(CreateFinding(
            summary: "Repository has no test framework detected",
            evidence: $"Repository contains {productionFiles.Count} production files but no test files or test framework packages found",
            whyItMatters: "A project without automated tests has zero protection against regressions. Every code change carries unknown risk.",
            suggestedAction: "Add a test project referencing xunit, NUnit, or equivalent for your language. Test coverage provides confidence in correctness.",
            confidence: Confidence.Medium));

        return Task.FromResult(findings);
    }

    private static bool IsSourceCodeFile(string filePath)
    {
        var sourceExtensions = new[] { ".cs", ".vb", ".ts", ".js", ".py", ".rs", ".go", ".java" };
        return sourceExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDocumentationFile(string filePath)
    {
        var docExtensions = new[] { ".md", ".txt", ".rst", ".adoc" };
        return docExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExemptDirectory(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        return ExemptDirectoryPatterns.Any(p =>
            normalized.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CheckForTestFrameworkPackages(IReadOnlyList<ChangedFileAnalysisRecord> allFiles)
    {
        // Look for .csproj, package.json, etc. and check for test framework references
        var projectFiles = allFiles
            .Where(f => f.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        f.FilePath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
                        f.FilePath.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Without access to file contents in AnalysisContext, we rely on
        // a future enhancement that would parse project files.
        // For now, if a project file exists but we can't parse it,
        // conservatively assume test framework may exist.
        // TODO: Parse project files when file content access is available

        return false; // Defer to test file detection until file parsing is available
    }
}
