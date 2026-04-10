// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0031Tests
{
    private static readonly GCI0031_BoundaryDrift Rule = new();

    [Fact]
    public async Task BoundaryComparisonWithoutTestEvidence_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Validator.cs b/src/Validator.cs
            index abc..def 100644
            --- a/src/Validator.cs
            +++ b/src/Validator.cs
            @@ -1,3 +1,5 @@
             public class Validator {
            +    if (value > 100) throw new ArgumentException("Too large");
            +    if (count < 0) throw new ArgumentException("Negative");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("100"));
    }

    [Fact]
    public async Task BoundaryComparisonWithTestEvidence_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Validator.cs b/src/Validator.cs
            index abc..def 100644
            --- a/src/Validator.cs
            +++ b/src/Validator.cs
            @@ -1,3 +1,4 @@
             public class Validator {
            +    if (value > 100) throw new ArgumentException("Too large");
             }
            diff --git a/src/ValidatorTests.cs b/src/ValidatorTests.cs
            index abc..def 100644
            --- a/src/ValidatorTests.cs
            +++ b/src/ValidatorTests.cs
            @@ -1,3 +1,4 @@
             public class ValidatorTests {
            +    [InlineData(100)] public void BoundaryTest(int v) { Assert.True(v <= 100); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("100"));
    }

    [Fact]
    public async Task NoBoundaryComparisons_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    var name = user.GetName();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
