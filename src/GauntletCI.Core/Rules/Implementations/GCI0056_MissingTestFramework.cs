// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0056, Missing Test Framework Detection
/// Detects repositories that appear to lack test infrastructure by scanning for
/// test framework references in project files and test file patterns.
/// </summary>
public class GCI0056_MissingTestFramework : RuleBase
{
    public GCI0056_MissingTestFramework(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0056";
    public override string Name => "Missing Test Framework";

    private static readonly string[] ProjectFilePatterns =
    [
        ".csproj", ".vbproj", "package.json", "pyproject.toml", "Cargo.toml", ".gradle",
    ];

    private static readonly string[] ExemptDirectoryPatterns =
    [
        "/samples/", "/sample/", "/examples/", "/example/", "/docs/", "/tools/", "/.github/",
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        var allFiles = context.EligibleFiles.Concat(context.SkippedFiles).ToList();

        var productionFiles = allFiles
            .Where(f => !WellKnownPatterns.IsTestFile(f.FilePath))
            .Where(f => !IsDocumentationFile(f.FilePath))
            .Where(f => !IsExemptDirectory(f.FilePath))
            .Where(f => IsSourceCodeFile(f.FilePath))
            .ToList();

        if (productionFiles.Count < 3)
            return Task.FromResult(findings);

        var hasProjectFile = allFiles.Any(f =>
            ProjectFilePatterns.Any(p => f.FilePath.EndsWith(p, StringComparison.OrdinalIgnoreCase)));

        if (!hasProjectFile)
            return Task.FromResult(findings);

        if (HasTestInfrastructureEvidence(allFiles))
            return Task.FromResult(findings);

        findings.Add(CreateFinding(
            summary: "Repository has no test framework detected",
            evidence: $"Repository contains {productionFiles.Count} production files but no test files or test framework packages found",
            whyItMatters: "A project without automated tests has zero protection against regressions. Every code change carries unknown risk.",
            suggestedAction: "Add a test project referencing xunit, NUnit, or equivalent for your language. Test coverage provides confidence in correctness.",
            confidence: Confidence.Medium));

        return Task.FromResult(findings);
    }

    private static bool HasTestInfrastructureEvidence(IReadOnlyList<ChangedFileAnalysisRecord> allFiles)
    {
        var testFiles = allFiles
            .Where(f => WellKnownPatterns.IsTestFile(f.FilePath))
            .Where(f => !f.FilePath.EndsWith("Benchmark.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.FilePath.EndsWith("Benchmarks.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (testFiles.Count > 0)
            return true;

        return TestFrameworkDetector.HasTestInfrastructure(Directory.GetCurrentDirectory());
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
}
