// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0009Tests
{
    private static readonly GCI0009_ConsistencyWithPatterns Rule = new();

    [Fact]
    public async Task MethodReturnsTaskButNotAsync_ShouldFlag()
    {
        // Context lines contain "async Task" so project uses async,
        // but new method returns Task without "async" keyword
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             // service
             public async Task DoExisting() { }
            +public Task<string> FetchData()
            +    => Task.FromResult("data");
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("not marked async"));
    }

    [Fact]
    public async Task AsyncMethodProperlyMarked_ShouldNotFlag()
    {
        // New method correctly marked async
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             // service
             public async Task DoExisting() { }
            +public async Task<string> FetchData()
            +    => await GetAsync();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("not marked async"));
    }

    [Fact]
    public async Task NoAsyncPattern_ShouldNotFlag()
    {
        // No context lines with "async Task" → rule should skip
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +public Task<string> FetchData() => Task.FromResult("x");
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("not marked async"));
    }
}
