// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0016Tests
{
    private static readonly GCI0016_ConcurrencyAndStateRisk Rule = new();

    private static DiffContext MakeDiff(string addedLine, string path = "src/Service.cs") =>
        DiffParser.Parse($"""
            diff --git a/{path} b/{path}
            index abc..def 100644
            --- a/{path}
            +++ b/{path}
            @@ -1,1 +1,2 @@
             // existing
            +{addedLine}
            """);

    // --- async void ---

    [Fact]
    public async Task AsyncVoidMethod_ShouldFlag()
    {
        var diff = MakeDiff("    public async void RunBackground() { }");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncVoidEventHandler_SenderEventArgs_ShouldNotFlag()
    {
        var diff = MakeDiff("    private async void OnClick(object sender, EventArgs e) { await DoWorkAsync(); }");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncVoidEventHandler_PropertyChangedArgs_ShouldNotFlag()
    {
        var diff = MakeDiff("    private async void OnChanged(object sender, PropertyChangedEventArgs e) { }");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncTaskMethod_ShouldNotFlag()
    {
        var diff = MakeDiff("    public async Task RunAsync() { }");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    // --- .Wait() / .GetAwaiter().GetResult() ---

    [Fact]
    public async Task DotWait_ShouldFlag()
    {
        var diff = MakeDiff("    task.Wait();");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains(".Wait()"));
    }

    [Fact]
    public async Task GetAwaiterGetResult_ShouldFlag()
    {
        var diff = MakeDiff("    var x = FetchAsync().GetAwaiter().GetResult();");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains("GetAwaiter"));
    }

    // --- .Result ---

    [Fact]
    public async Task DotResultChainedOnMethodCall_ShouldFlag()
    {
        // .Result directly on a method call result — clear blocking pattern.
        var diff = MakeDiff("    var result = GetDataAsync().Result;");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task DotResultWithTaskContext_ShouldFlag()
    {
        // Task<T> variable with .Result — explicit Task type context.
        var diff = MakeDiff("    var x = Task.Run(() => Compute()).Result;");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task DotResultOnDomainProperty_ShouldNotFlag()
    {
        // 'Result' is a domain property (OperationResult, HttpResult, etc.) — not a Task.
        var diff = MakeDiff("    var code = response.Result.StatusCode;");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task DotResultOnExceptionPayload_ShouldNotFlag()
    {
        // Common in test assertions: exception.Result.Should().Be(10)
        var diff = MakeDiff("    exception.Result.Should().Be(10);");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task AwaitedExpression_ShouldNotFlag()
    {
        var diff = MakeDiff("    await task.ConfigureAwait(false);");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task CommentedDotResult_ShouldNotFlag()
    {
        var diff = MakeDiff("    // var result = task.Result;");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    [Fact]
    public async Task MethodNameContainingGetResult_ShouldNotFlag()
    {
        var diff = MakeDiff("    var x = await FetchAndGetResultAsync();");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result"));
    }

    // --- lock(this) ---

    [Fact]
    public async Task LockThis_ShouldFlag()
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

    // --- Thread.Sleep ---

    [Fact]
    public async Task ThreadSleep_InProductionCode_ShouldFlag()
    {
        var diff = MakeDiff("    Thread.Sleep(500);");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.Contains(findings, f => f.Summary.Contains("Thread.Sleep"));
    }

    [Fact]
    public async Task ThreadSleep_InTestFile_ShouldNotFlag()
    {
        var diff = MakeDiff("    Thread.Sleep(100);", "tests/MyServiceTests.cs");
        var findings = await Rule.EvaluateAsync(diff, null);
        Assert.DoesNotContain(findings, f => f.Summary.Contains("Thread.Sleep"));
    }
}
