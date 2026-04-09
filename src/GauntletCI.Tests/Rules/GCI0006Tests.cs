// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0006Tests
{
    private static readonly GCI0006_EdgeCaseHandling Rule = new();

    [Fact]
    public async Task ValueAccessWithoutNullGuard_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +var result = maybe.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task ValueAccessWithNullGuard_ShouldNotFlag()
    {
        // .HasValue check in a preceding added line
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,3 @@
             // existing
            +if (maybe.HasValue)
            +    var result = maybe.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task PublicMethodWithStringParam_ShouldFlag()
    {
        // Public method with string param, no null check in next lines
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(string input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithStringParam_WithNullCheck_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,4 @@
             // existing
            +public void Process(string input)
            +{
            +    ArgumentNullException.ThrowIfNull(input);
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }
}
