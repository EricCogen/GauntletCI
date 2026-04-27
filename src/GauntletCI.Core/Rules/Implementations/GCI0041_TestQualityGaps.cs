// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0041 – Test Quality Gaps
/// Detects low-quality test patterns: silenced tests, uninformative method names, and missing assertions.
/// Only evaluates files whose path contains "test" or "spec" (case-insensitive).
/// </summary>
public class GCI0041_TestQualityGaps : RuleBase
{
    public override string Id => "GCI0041";
    public override string Name => "Test Quality Gaps";

    private static readonly string[] SilencePatterns =
        ["[Ignore", "[Skip", ".Skip(", "[Fact(Skip", "[Theory(Skip"];

    private static readonly string[] TestAttributeMarkers =
        ["[Fact]", "[Theory]", "[Test]"];

    private static readonly HashSet<string> BadMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Test1", "Test2", "TestMethod", "TestMethod1", "Method1", "Verify", "Check", "DoTest"
    };

    private static readonly Regex BadNamePattern =
        new(@"^[Tt]est\d+$|^[Mm]ethod\d+$", RegexOptions.Compiled);

    private static readonly Regex MethodNameRegex =
        new(@"\b(?:public|private|protected|internal)\s+(?:async\s+)?(?:Task|void|[\w<>]+)\s+(\w+)\s*\(", RegexOptions.Compiled);

    private static readonly string[] AssertionKeywords =
    [
        // xUnit / NUnit / MSTest
        "Assert.", "Xunit.Assert", "NUnit.Framework.Assert",
        // FluentAssertions / Shouldly
        "Should", ".ShouldBe", ".ShouldNotBe", ".ShouldBeNull", ".ShouldNotBeNull",
        ".Must(",
        // NSubstitute
        "Received(", "DidNotReceive(",
        // Moq / FakeItEasy
        ".Verify(", ".VerifyAll(", "MustHaveHappened", "MustNotHaveHappened",
        // Common assertion patterns
        "Throws<", "DoesNotThrow", "ThrowsAsync", "expect(", "Expect(",
        "IsTrue(", "IsFalse(", "IsNull(", "IsNotNull(", "AreEqual(", "AreNotEqual(",
        "Contains(", "IsInstanceOf",
    ];

    // Matches custom assertion helpers: AssertValid(...), VerifyResult(...), CheckState(...)
    // and methods ending in those words: ResultAssert(...), SomeVerify(...)
    private static readonly Regex AssertionHelperRegex =
        new(@"\b(?:Assert|Verify|Check)\w+\s*\(|\b\w+(?:Assert|Verify|Check)\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(IsTestFile))
        {
            CheckSilencedTests(file, findings);
            CheckUninformativeTestNames(file, findings);
            CheckEmptyAssertions(file, findings);
        }

        return Task.FromResult(findings);
    }

    private static bool IsTestFile(DiffFile file)
    {
        var path = file.NewPath;
        return path.Contains("test", StringComparison.OrdinalIgnoreCase)
            || path.Contains("spec", StringComparison.OrdinalIgnoreCase);
    }

    private void CheckSilencedTests(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            foreach (var pattern in SilencePatterns)
            {
                if (!content.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Test silenced with Skip/Ignore attribute",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Silenced tests give false confidence that the suite is green while real failures go undetected.",
                    suggestedAction: "Fix the underlying issue and re-enable the test. If the test is permanently obsolete, delete it.",
                    confidence: Confidence.Medium,
                    line: line));
                break;
            }
        }
    }

    private void CheckUninformativeTestNames(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;

            var content = line.Content.Trim();
            if (!TestAttributeMarkers.Any(a => content.Equals(a, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Find the method name on the next non-blank line
            string? methodName = null;
            for (int j = i + 1; j < Math.Min(allLines.Count, i + 5); j++)
            {
                var nextContent = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(nextContent)) continue;

                var match = MethodNameRegex.Match(nextContent);
                if (match.Success)
                    methodName = match.Groups[1].Value;
                break;
            }

            if (methodName is null) continue;

            if (!BadMethodNames.Contains(methodName) && !BadNamePattern.IsMatch(methodName)) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Uninformative test method name",
                evidence: $"Line {line.LineNumber}: method '{methodName}' has a low-signal name",
                whyItMatters: "Tests named 'Test1' or 'TestMethod' provide no documentation value and make failures hard to diagnose.",
                suggestedAction: "Use descriptive names following the pattern: MethodName_Scenario_ExpectedBehavior.",
                confidence: Confidence.Low,
                line: line));
        }
    }

    private void CheckEmptyAssertions(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        bool hasTestAttribute = addedLines.Any(l =>
            TestAttributeMarkers.Any(a => l.Content.Trim().Equals(a, StringComparison.OrdinalIgnoreCase)));

        if (!hasTestAttribute) return;

        // Check both added lines and context lines — assertions may live in helper calls
        // or in lines that weren't changed in this diff.
        var allVisibleLines = file.Hunks.SelectMany(h => h.Lines)
            .Where(l => l.Kind != DiffLineKind.Removed)
            .Select(l => l.Content)
            .ToList();

        bool hasAssertion = allVisibleLines.Any(l =>
            AssertionKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
            AssertionHelperRegex.IsMatch(l));

        if (hasAssertion) return;

        findings.Add(CreateFinding(
            file,
            summary: "Test method may lack assertions",
            evidence: "A test attribute was added but no assertion keywords were found in the added lines.",
            whyItMatters: "Tests without assertions always pass and provide no safety net against regressions.",
            suggestedAction: "Add at least one assertion to verify the expected behavior.",
            confidence: Confidence.Low));
    }
}
