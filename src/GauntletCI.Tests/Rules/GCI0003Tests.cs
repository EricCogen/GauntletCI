// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
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
}
