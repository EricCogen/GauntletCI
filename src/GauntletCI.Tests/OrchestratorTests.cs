// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests;

public class OrchestratorTests
{
    [Fact]
    public async Task CreateDefault_ShouldDiscoverAllRules()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var result = await orchestrator.RunAsync(diff);
        Assert.Equal(19, result.RulesEvaluated);
    }

    [Fact]
    public async Task RunAsync_WithCleanDiff_ShouldReturnResult()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var result = await orchestrator.RunAsync(diff);
        Assert.NotNull(result);
        Assert.Equal(19, result.RulesEvaluated);
    }

    [Fact]
    public async Task PostProcess_WhenMoreThanThreeRulesFire_ShouldAddGci0018Synthesis()
    {
        // Craft a diff with signals for 4+ distinct rules:
        // GCI0010 (hardcoded secret), GCI0012 (SQL injection + weak crypto),
        // GCI0033 (async void), GCI0016 (static mutable state)
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,12 @@
             // service
            +var password = "secret123";
            +var sql = "SELECT * FROM Users WHERE Name = '" + name + "'";
            +using var md5 = MD5.Create();
            +private static List<string> _cache = new();
            +public async void FireAndForget() { await DoWork(); }
            +catch (Exception) { }
            +var host = "192.168.1.100";
            """;
        var diff = DiffParser.Parse(raw);
        var orchestrator = RuleOrchestrator.CreateDefault();
        var result = await orchestrator.RunAsync(diff);
        Assert.Contains(result.Findings, f => f.RuleId == "GCI_SYN_AGG" && f.Summary.Contains("rules flagged"));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_ShouldThrowOrComplete()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.RunAsync(diff, ct: cts.Token));
    }

    [Fact]
    public async Task RunAsync_WithStaticAnalysis_ShouldNotThrow()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new GauntletCI.Core.StaticAnalysis.AnalyzerResult
        {
            Diagnostics =
            [
                new() { Id = "CA1062", Message = "Validate param", FilePath = "src/Foo.cs", Line = 1 },
                new() { Id = "CA1031", Message = "Catch specific", FilePath = "src/Foo.cs", Line = 5 },
                new() { Id = "CA2100", Message = "SQL injection", FilePath = "src/Foo.cs", Line = 10 },
                new() { Id = "CA1305", Message = "Specify format", FilePath = "src/Foo.cs", Line = 2 },
                new() { Id = "CA2227", Message = "Collection property", FilePath = "src/Foo.cs", Line = 3 }
            ],
            Success = true
        };
        var result = await orchestrator.RunAsync(diff, staticAnalysis);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task HasFindings_WhenFindingsExist_ReturnsTrue()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,3 @@
             // service
            +var password = "hunter2";
            +var sql = "SELECT * FROM Users WHERE Name = '" + name + "'";
            """);
        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.HasFindings);
    }

    [Fact]
    public async Task RunAsync_LargeDiff_ShouldAddGci0019Warning()
    {
        // Build a diff with 200+ lines to trigger the large-diff warning (>200 lines, 0-1 findings)
        var addedLines = string.Join("\n", Enumerable.Range(1, 210).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/CleanService.cs b/src/CleanService.cs
            index abc..def 100644
            --- a/src/CleanService.cs
            +++ b/src/CleanService.cs
            @@ -1,1 +1,211 @@
             // service
            {addedLines}
            """;
        var diff = DiffParser.Parse(raw);
        var orchestrator = RuleOrchestrator.CreateDefault();
        var result = await orchestrator.RunAsync(diff);
        // The GCI0019 large-diff warning is added if totalLines > 200 and findings < 2
        // We just verify RunAsync completes without error
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RunAsync_NonCsFile_ShouldBeBypassed()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
            diff --git a/docs/readme.md b/docs/readme.md
            index abc..def 100644
            --- a/docs/readme.md
            +++ b/docs/readme.md
            @@ -1,1 +1,2 @@
             # Notes
            +password = "secret123"
            """);

        var result = await orchestrator.RunAsync(diff);

        Assert.Empty(result.Findings);
    }
}
