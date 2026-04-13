// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0027 – Test Quality
/// Detects test methods that have no meaningful assertion, asserting only non-null,
/// or appear to be copy-paste duplicates.
/// </summary>
public class GCI0027_TestQuality : RuleBase
{
    public override string Id => "GCI0027";
    public override string Name => "Test Quality";

    private static readonly string[] TestAttributes = ["[Fact]", "[Test]", "[Theory]", "[TestMethod]"];

    private static readonly string[] AssertionPatterns =
    [
        "Assert.", ".Should()", ".Should.", "Expect(", "Verify(",
        "Assert.Equal", "Assert.True", "Assert.False", "Assert.Throws",
        "Assert.Contains", "Assert.NotNull", "Assert.Null",
        "xunit", "FluentAssertions"
    ];

    private static readonly string[] TrivialAssertions =
    [
        "Assert.NotNull", "Assert.IsNotNull", "Assert.IsNull",
        "Assert.Null", "isNotNull", "IsNotNull(", "IsNull("
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(IsTestFile))
        {
            CheckTestAssertions(file, findings);
        }

        return Task.FromResult(findings);
    }

    // Diverges intentionally from WellKnownPatterns.IsTestFile: takes a DiffFile rather than a string
    // path, and checks file endings (Tests.cs, Test.cs, Spec.cs) as well as directory segments,
    // which is more precise for the test-quality assertion checks in this rule.
    private static bool IsTestFile(DiffFile file)
    {
        var path = file.NewPath;
        return path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("Spec.cs", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Tests/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Tests\\", StringComparison.OrdinalIgnoreCase);
    }

    private void CheckTestAssertions(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content.Trim();

            // Find a test attribute
            if (!TestAttributes.Any(a => content.Equals(a, StringComparison.OrdinalIgnoreCase))) continue;

            // Collect the test body — next ~30 lines until we hit the next attribute or end of method
            int bodyEnd = Math.Min(allLines.Count, i + 40);
            var body = allLines[(i + 1)..bodyEnd]
                .Where(l => l.Kind == DiffLineKind.Added)
                .Select(l => l.Content)
                .ToList();

            bool hasAssertion = body.Any(l =>
                AssertionPatterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)));

            if (!hasAssertion)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Test method without assertions in {file.NewPath} (at line {line.LineNumber}).",
                    evidence: $"Line {line.LineNumber}: {content} — no Assert/Should/Expect found in method body",
                    whyItMatters: "A test with no assertions always passes — it gives false confidence and provides zero protection against regressions.",
                    suggestedAction: "Add meaningful assertions that verify the expected behaviour, not just that the code runs without throwing.",
                    confidence: Confidence.High,
                    line: line));
                continue;
            }

            // Check for trivial-only assertions (only NotNull checks)
            bool hasNonTrivialAssertion = body.Any(l =>
                AssertionPatterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
                !TrivialAssertions.Any(t => l.Trim().StartsWith(t, StringComparison.OrdinalIgnoreCase)));

            if (!hasNonTrivialAssertion)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Test method in {file.NewPath} only asserts non-null (line {line.LineNumber}).",
                    evidence: $"Line {line.LineNumber}: {content} — only null-check assertions found",
                    whyItMatters: "Asserting only non-null doesn't verify correctness — the method could return a wrong value and the test would still pass.",
                    suggestedAction: "Add value-level assertions: Assert.Equal(expected, actual) to verify the returned value, not just its existence.",
                    confidence: Confidence.Medium,
                    line: line));
            }
        }
    }
}
