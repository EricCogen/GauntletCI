// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests;

/// <summary>
/// Tests for WellKnownPatterns.IsTestFile behavior, exercised indirectly through
/// GCI0048 which calls IsTestFile to skip test files.
/// Key regression: lower.Contains("test") caused false positives on production files
/// like LatestOrderService.cs and ContestController.cs.
/// </summary>
public class WellKnownPatternsTests
{
    private static readonly GCI0048_InsecureRandomInSecurityContext Rule = new();

    // Helper: a diff that would fire GCI0048 (new Random near "token")
    private static string MakeDiff(string filePath) => $$"""
        diff --git a/{{filePath}} b/{{filePath}}
        index abc..def 100644
        --- a/{{filePath}}
        +++ b/{{filePath}}
        @@ -1,3 +1,6 @@
         public class Foo {
        +    string GenerateToken() {
        +        var rng = new Random();
        +        return rng.Next().ToString();
        +    }
         }
        """;

    // ── Production files that embed "test" mid-word: must NOT be skipped ──

    [Theory]
    [InlineData("src/Services/LatestOrderService.cs")]     // "latest" contains "test" mid-word
    [InlineData("src/Controllers/ContestController.cs")]   // "contest" contains "test" mid-word
    [InlineData("src/Services/ProtestService.cs")]         // "protest" contains "test" mid-word
    public async Task ProductionFileWithEmbeddedTestWord_ShouldNotBeSkipped(string path)
    {
        var diff = DiffParser.Parse(MakeDiff(path));
        var findings = await Rule.EvaluateAsync(diff, null);

        // Should fire — this is a production file, not a test file
        Assert.NotEmpty(findings);
    }

    // ── Actual test files: must be skipped ────────────────────────────────

    [Theory]
    [InlineData("src/Tests/Foo.cs")]          // "Tests" directory segment
    [InlineData("FooTests.cs")]               // file name ends with "Tests"
    [InlineData("FooTest.cs")]                // file name ends with "Test"
    [InlineData("TestFooService.cs")]         // file name starts with "Test"
    public async Task ActualTestFile_ShouldBeSkipped(string path)
    {
        var diff = DiffParser.Parse(MakeDiff(path));
        var findings = await Rule.EvaluateAsync(diff, null);

        // Should not fire — rule skips test files
        Assert.Empty(findings);
    }
}
