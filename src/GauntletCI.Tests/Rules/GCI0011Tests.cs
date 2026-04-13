// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0011Tests
{
    private static readonly GCI0011_PerformanceRisk Rule = new();

    [Fact]
    public async Task ToListInsideLoop_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +foreach (var item in items)
            +{
            +    var snapshot = data.ToList();
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Materializing collection inside loop"));
    }

    [Fact]
    public async Task CountGreaterThanZero_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +if (items.Count() > 0) { DoWork(); }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Any()"));
    }

    [Fact]
    public async Task ThreadSleep_ShouldNotFlag_OwnerIsGCI0016()
    {
        // Thread.Sleep detection is owned by GCI0016 (Concurrency and State Risk).
        // GCI0011 must not duplicate this check.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +Thread.Sleep(1000);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Thread.Sleep()"));
    }

    [Fact]
    public async Task BlockingResultCall_ShouldNotFlag_OwnerIsGCI0016()
    {
        // .Result / .GetAwaiter().GetResult() detection is owned by GCI0016.
        // GCI0011 must not duplicate this check.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +var data = GetDataAsync().GetAwaiter().GetResult();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Blocking async call"));
    }

    [Fact]
    public async Task NewListInsideLoop_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +foreach (var item in items)
            +{
            +    var batch = new List<string>();
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("allocated inside loop"));
    }

    [Fact]
    public async Task StringConcatInLoop_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +foreach (var item in items)
            +{
            +    result += item + ",";
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("String concatenation in loop"));
    }
}
