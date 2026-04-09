// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0008Tests
{
    private static readonly GCI0008_ComplexityControl Rule = new();

    [Fact]
    public async Task DeeplyNestedCode_ShouldFlag()
    {
        // 5+ levels of { nesting in added lines
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,10 @@
             // service
            +public void Method() {
            +    if (a) {
            +        foreach (var x in list) {
            +            if (b) {
            +                while (c) {
            +                    DoWork();
            +                }
            +            }
            +        }
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Nesting depth"));
    }

    [Fact]
    public async Task DuplicateLines_ShouldFlag()
    {
        // Same 11+ char line added 3 times
        const string dup = "entity.Name = request.Name;";
        var raw = $"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,4 @@
             // service
            +{dup}
            +{dup}
            +{dup}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("appear 3+ times") || f.Summary.Contains("3+"));
    }

    [Fact]
    public async Task ShallowNesting_ShouldNotFlagNesting()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +public void Method() {
            +    if (a) {
            +        DoWork();
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Nesting depth"));
    }

    [Fact]
    public async Task LargeAddedMethod_ShouldFlagLongMethod()
    {
        // Build a diff with 31+ added lines inside a method block
        var lines = string.Join("\n", Enumerable.Range(1, 31).Select(i => $"+    var x{i} = {i};"));
        var raw = $$"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,33 @@
             // service
            +public void BigMethod() {
            {{lines}}
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Large method block") || f.Summary.Contains("added lines"));
    }
}
