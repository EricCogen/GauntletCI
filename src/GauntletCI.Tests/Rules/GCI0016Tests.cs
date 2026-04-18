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

    [Fact]
    public async Task DotWaitOnTask_ShouldFlagFinding()
    {
        var diff = MakeDiff("    task.Wait();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Wait()"));
    }

    [Fact]
    public async Task ConfigureAwaitFalse_ShouldNotFlag()
    {
        var diff = MakeDiff("    await task.ConfigureAwait(false);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("ConfigureAwait"));
    }

    [Fact]
    public async Task AsyncTaskMethod_ShouldNotFlag()
    {
        var diff = MakeDiff("    public async Task RunAsync() { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task GetResultInsideAwait_ShouldNotFlagFalsePositive()
    {
        // "GetResult" as part of a method name, not .Result access
        var diff = MakeDiff("    var x = await FetchAndGetResultAsync();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task CommentedDotResult_ShouldNotFlag()
    {
        var diff = MakeDiff("    // var result = task.Result;");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task LockThis_ShouldFlagWarning()
    {
        var diff = MakeDiff("    lock (this) { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("lock(this)"));
    }

    [Fact]
    public async Task LockOnPrivateField_ShouldNotFlag()
    {
        var diff = MakeDiff("    lock (_syncRoot) { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("lock"));
    }

    [Fact]
    public async Task AsyncVoidEventHandlerWithSender_ShouldNotFlag()
    {
        var diff = MakeDiff("    private async void OnClick(object sender, EventArgs e) { await DoWorkAsync(); }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncVoidEventHandlerWithEventArgs_ShouldNotFlag()
    {
        var diff = MakeDiff("    private async void OnChanged(object sender, PropertyChangedEventArgs e) { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }
}
