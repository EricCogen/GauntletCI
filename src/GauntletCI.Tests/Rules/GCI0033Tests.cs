// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

/// <summary>
/// GCI0033 is now superseded by GCI0016 (Concurrency and State Risk), which already
/// detects .Result/.Wait() blocking calls with equivalent guidance. These tests validate
/// the absorbed behavior via GCI0016, and verify GCI0033 itself no longer produces findings.
/// </summary>
public class GCI0033Tests
{
    private static readonly GCI0016_ConcurrencyAndStateRisk Rule = new();
    private static readonly GCI0033_AsyncSinkhole SupersededRule = new();

    [Fact]
    public async Task GCI0033_IsSuperseded_ShouldReturnNoFindings()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    var data = GetDataAsync().Result;
             }
            """;
        var diff = DiffParser.Parse(raw);
        var findings = await SupersededRule.EvaluateAsync(diff, null);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task DotResultAccess_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    var data = GetDataAsync().Result;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Result") && f.Summary.Contains("deadlock"));
    }

    [Fact]
    public async Task DotWaitCall_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    ProcessAsync().Wait();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Wait()") && f.Summary.Contains("deadlock"));
    }

    [Fact]
    public async Task AwaitUsed_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    var data = await GetDataAsync();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Result") || f.Summary.Contains(".Wait()"));
    }

    [Fact]
    public async Task WaitOneCall_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    resetEvent.WaitOne(timeout);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("deadlock"));
    }

    [Fact]
    public async Task CommentedDotResult_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    // var data = old.Result;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("deadlock"));
    }
}
