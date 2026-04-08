// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0016Tests
{
    private static readonly GCI0016_ConcurrencyAndStateRisk Rule = new();

    private static DiffContext MakeDiff(string addedLine) =>
        DiffParser.Parse($"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +{addedLine}
            """);

    [Fact]
    public async Task AsyncVoidMethod_ShouldFlagFinding()
    {
        var diff = MakeDiff("    public async void RunBackground() { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task DotResultOnTask_ShouldFlagFinding()
    {
        var diff = MakeDiff("    var result = GetDataAsync().Result;");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Result"));
    }
}
