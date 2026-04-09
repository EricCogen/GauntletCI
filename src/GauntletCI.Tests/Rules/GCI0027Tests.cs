// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0027Tests
{
    private static readonly GCI0027_TestQuality Rule = new();

    [Fact]
    public async Task TestMethodWithNoAssertions_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/FooTests.cs b/src/FooTests.cs
            index abc..def 100644
            --- a/src/FooTests.cs
            +++ b/src/FooTests.cs
            @@ -1,3 +1,8 @@
             public class FooTests {
            +    [Fact]
            +    public async Task RunsWithoutError()
            +    {
            +        var result = await _service.RunAsync();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("without assertions") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task TestMethodWithOnlyNotNullAssertion_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/BarTests.cs b/src/BarTests.cs
            index abc..def 100644
            --- a/src/BarTests.cs
            +++ b/src/BarTests.cs
            @@ -1,3 +1,9 @@
             public class BarTests {
            +    [Fact]
            +    public async Task GetUser_ReturnsNonNull()
            +    {
            +        var user = await _svc.GetUserAsync(1);
            +        Assert.NotNull(user);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("only asserts non-null") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task TestMethodWithAssertEqual_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/BazTests.cs b/src/BazTests.cs
            index abc..def 100644
            --- a/src/BazTests.cs
            +++ b/src/BazTests.cs
            @@ -1,3 +1,9 @@
             public class BazTests {
            +    [Fact]
            +    public async Task GetUser_ReturnsCorrectName()
            +    {
            +        var user = await _svc.GetUserAsync(1);
            +        Assert.Equal("Alice", user.Name);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FactInNonTestFile_ShouldNotFlag()
    {
        // Rule only scans files matching test file patterns
        var raw = """
            diff --git a/src/ProductionCode.cs b/src/ProductionCode.cs
            index abc..def 100644
            --- a/src/ProductionCode.cs
            +++ b/src/ProductionCode.cs
            @@ -1,3 +1,7 @@
             public class ProductionCode {
            +    [Fact]
            +    public void MisplacedTestAttr()
            +    {
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
