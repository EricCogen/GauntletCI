// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0049Tests
{
    private static readonly GCI0049_FloatDoubleEqualityComparison Rule = new();

    [Fact]
    public async Task DoubleEqualityWithLiteral_ShouldFire()
    {
        var raw = """
            diff --git a/src/Calc/Calculator.cs b/src/Calc/Calculator.cs
            index abc..def 100644
            --- a/src/Calc/Calculator.cs
            +++ b/src/Calc/Calculator.cs
            @@ -1,4 +1,6 @@
             public class Calculator {
            +    public bool IsZero(double value) {
            +        if (value == 0.0) return true;
            +        return false;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.RuleId == "GCI0049");
    }

    [Fact]
    public async Task FloatNotEqualsWithLiteral_ShouldFire()
    {
        var raw = """
            diff --git a/src/Physics/Simulation.cs b/src/Physics/Simulation.cs
            index abc..def 100644
            --- a/src/Physics/Simulation.cs
            +++ b/src/Physics/Simulation.cs
            @@ -5,4 +5,5 @@
             class Simulation {
            +    bool IsMoving(float velocity) => velocity != 0.0f;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task FloatCastWithEquality_ShouldFire()
    {
        var raw = """
            diff --git a/src/Core/Converter.cs b/src/Core/Converter.cs
            index abc..def 100644
            --- a/src/Core/Converter.cs
            +++ b/src/Core/Converter.cs
            @@ -3,4 +3,5 @@
             class Converter {
            +    bool Check(object x) => (float)x == 1.5f;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task IntegerEquality_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Core/Counter.cs b/src/Core/Counter.cs
            index abc..def 100644
            --- a/src/Core/Counter.cs
            +++ b/src/Core/Counter.cs
            @@ -3,4 +3,5 @@
             class Counter {
            +    bool IsZero(int count) => count == 0;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task StringEquality_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Core/Validator.cs b/src/Core/Validator.cs
            index abc..def 100644
            --- a/src/Core/Validator.cs
            +++ b/src/Core/Validator.cs
            @@ -3,4 +3,5 @@
             class Validator {
            +    bool IsEmpty(string s) => s == "";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FloatEqualityInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Tests/CalcTests.cs b/src/Tests/CalcTests.cs
            index abc..def 100644
            --- a/src/Tests/CalcTests.cs
            +++ b/src/Tests/CalcTests.cs
            @@ -3,4 +3,6 @@
             public class CalcTests {
            +    [Fact]
            +    public void IsZero_WhenZero() {
            +        Assert.True(0.0 == 0.0);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task DecimalEquality_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Finance/PriceChecker.cs b/src/Finance/PriceChecker.cs
            index abc..def 100644
            --- a/src/Finance/PriceChecker.cs
            +++ b/src/Finance/PriceChecker.cs
            @@ -3,4 +3,5 @@
             class PriceChecker {
            +    bool IsAtParity(decimal price) => price == 1.0m;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FloatAndStringEqualityOnSameLine_ShouldFire()
    {
        // The float comparison (value == 0.0) should be detected even though
        // the same line also has an unrelated string comparison (name == "x")
        var raw = """
            diff --git a/src/Core/Checker.cs b/src/Core/Checker.cs
            index abc..def 100644
            --- a/src/Core/Checker.cs
            +++ b/src/Core/Checker.cs
            @@ -3,4 +3,5 @@
             class Checker {
            +    bool Check(double value, string name) => value == 0.0 && name == "x";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.RuleId == "GCI0049");
    }

    [Fact]
    public async Task CommentLine_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Core/Math.cs b/src/Core/Math.cs
            index abc..def 100644
            --- a/src/Core/Math.cs
            +++ b/src/Core/Math.cs
            @@ -3,4 +3,5 @@
             class MathHelper {
            +    // if (x == 0.0) is a float equality bug
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
