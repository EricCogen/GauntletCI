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
    public async Task PublicMethodWithNullableStringParam_ShouldFlag()
    {
        // Public method with nullable string? param and no null check — should flag
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(string? input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithNonNullableStringParam_ShouldNotFlag()
    {
        // Non-nullable string is compiler-enforced in nullable-enabled C# — no guard needed
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

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithNullableStringParam_WithNullCheck_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,4 @@
             // existing
            +public void Process(string? input)
            +{
            +    ArgumentNullException.ThrowIfNull(input);
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task ValueInCommentLine_ShouldNotFlag()
    {
        // .Value inside a code comment — not executable
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +// match.Value shows the full attribute in findings
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task PrivateMethodWithStringParam_ShouldNotFlag()
    {
        // Private methods — callers are controlled, no need for null guards
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +private void Helper(string input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodStringReturnType_NoStringParam_ShouldNotFlag()
    {
        // "string" in return type only — no string parameter
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public string BuildBody(List<Finding> findings, bool hasInline)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodInTestFile_ShouldNotFlag()
    {
        // Test file helpers don't need null guards
        var raw = """
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,1 +1,3 @@
             // existing
            +private static Finding MakeFinding(string ruleId = "GCI0001")
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }
}
