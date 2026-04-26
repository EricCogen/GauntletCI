// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0003Tests
{
    private static readonly GCI0003_BehavioralChangeDetection Rule = new();

    [Fact]
    public async Task RemovedLogicWithoutTests_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,7 +1,3 @@
             public int Compute(int x) {
            -    if (x < 0) throw new ArgumentException("negative");
            -    if (x == 0) return 0;
            -    return x * 2;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    [Fact]
    public async Task ChangedMethodSignature_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,3 @@
             // service class
            -public void DoWork(int x)
            +public void DoWork(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task PrivateMethodSignatureChange_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -private void Helper(int x)
            +private void Helper(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task PublicMethodOnlyOptionalParamsAdded_ShouldFlagAsLowConfidence()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -public void DoWork(int x)
            +public void DoWork(int x, string label = "default", bool verbose = false)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings, f => f.Summary.Contains("Backward-compatible", StringComparison.Ordinal));
        Assert.Equal(Confidence.Low, f.Confidence);
    }

    [Fact]
    public async Task PublicMethodRequiredParamAdded_ShouldFlagAsMediumConfidence()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -public void DoWork(int x)
            +public void DoWork(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings, f => f.Summary.Contains("signature changed"));
        Assert.Equal(Confidence.Medium, f.Confidence);
    }

    [Fact]
    public async Task RemovedLogicWithTestChanges_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,2 @@
             public int Compute() {
            -    return x * 2;
             }
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,1 +1,2 @@
             // test
            +Assert.Equal(0, svc.Compute());
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    [Fact]
    public async Task ExpressionBodyChange_ShouldNotFlagSignatureChange()
    {
        // Only the expression body changes — the signature (name + params) is identical.
        var raw = """
            diff --git a/src/Core/Checker.cs b/src/Core/Checker.cs
            index abc..def 100644
            --- a/src/Core/Checker.cs
            +++ b/src/Core/Checker.cs
            @@ -1,3 +1,3 @@
             public class Checker {
            -    public bool HasModifier(string content) => content.Contains("internal ");
            +    public bool HasModifier(string content) => content.Contains("internal ", StringComparison.Ordinal);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task AccessModifierInStringLiteralWithParen_ShouldNotFlagSignatureChange()
    {
        // A non-signature line containing "internal " in a string plus "(" should not
        // be treated as a method signature (exercises TrimStart().StartsWith() guard).
        var raw = """
            diff --git a/src/Core/Checker.cs b/src/Core/Checker.cs
            index abc..def 100644
            --- a/src/Core/Checker.cs
            +++ b/src/Core/Checker.cs
            @@ -1,3 +1,3 @@
             public class Checker {
            -    var msg = "internal method(old)";
            +    var msg = "internal method(new)";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task AttributeDecoratedMethod_ShouldFlagSignatureChange()
    {
        // [Obsolete] attribute before "public" should not prevent signature detection.
        var raw = """
            diff --git a/src/Core/Api.cs b/src/Core/Api.cs
            index abc..def 100644
            --- a/src/Core/Api.cs
            +++ b/src/Core/Api.cs
            @@ -1,3 +1,3 @@
             public class Api {
            -    [Obsolete] public void Process(string input) { }
            +    [Obsolete] public void Process(string input, int timeout) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task GenericConstraintChange_ShouldFlagSignatureChange()
    {
        // A where-clause change is a signature-level breaking change and must be flagged.
        var raw = """
            diff --git a/src/Core/Api.cs b/src/Core/Api.cs
            index abc..def 100644
            --- a/src/Core/Api.cs
            +++ b/src/Core/Api.cs
            @@ -1,3 +1,3 @@
             public class Api {
            -    public void Process<T>(T input) where T : struct { }
            +    public void Process<T>(T input) where T : class { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }
}
