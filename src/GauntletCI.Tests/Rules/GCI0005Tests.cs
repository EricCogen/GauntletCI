// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0005Tests
{
    private static readonly GCI0005_TestCoverageRelevance Rule = new();

    [Fact]
    public async Task CodeChangedWithNoTests_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("no test file"));
    }

    [Fact]
    public async Task TestOnlyChange_ShouldFlagOrphaned()
    {
        var raw = """
            diff --git a/src/FooTests.cs b/src/FooTests.cs
            index abc..def 100644
            --- a/src/FooTests.cs
            +++ b/src/FooTests.cs
            @@ -1,1 +1,2 @@
             // test
            +Assert.True(true);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("no corresponding production code"));
    }

    [Fact]
    public async Task CodeAndTestsChanged_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            diff --git a/src/FooTests.cs b/src/FooTests.cs
            index abc..def 100644
            --- a/src/FooTests.cs
            +++ b/src/FooTests.cs
            @@ -1,1 +1,2 @@
             // test
            +Assert.Equal(1, foo.X);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
