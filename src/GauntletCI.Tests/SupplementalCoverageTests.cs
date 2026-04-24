// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Tests;

/// <summary>
/// Supplemental tests targeting uncovered branches in various rules and infrastructure.
/// </summary>
public class SupplementalCoverageTests
{
    // ── GCI0001 whitespace churn ─────────────────────────────────────────────

    [Fact]
    public async Task GCI0001_WhitespaceChurn_ShouldFlag()
    {
        // Build a diff with >10 total changes and >40% whitespace-only added lines
        var rule = new GCI0001_DiffIntegrity();
        // 12 added lines: 6 are whitespace-only (50% > 40%), total changed = 12 > 10
        var addedLines = new string[]
        {
            "+    ",
            "+    ",
            "+    ",
            "+    ",
            "+    ",
            "+    ",
            "+int a = 1;",
            "+int b = 2;",
            "+int c = 3;",
            "+int d = 4;",
            "+int e = 5;",
            "+int f = 6;"
        };
        var raw = $"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,13 @@
             // service
            {string.Join("\n", addedLines)}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("whitespace") || f.Summary.Contains("formatting"));
    }

    // ── GCI0007 catch with non-throw, non-log content ────────────────────────

    [Fact]
    public async Task GCI0007_CatchWithMeaningfulContent_ShouldNotFlag()
    {
        // A catch block that has content (not throw/log), so hasContent=true, not swallowed
        var rule = new GCI0007_ErrorHandlingIntegrity();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (Exception ex)
            +{
            +    errorCount++;
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    // ── GCI0010 additional patterns ───────────────────────────────────────────

    [Fact]
    public async Task GCI0010_HardcodedUrl_ShouldFlag()
    {
        var rule = new GCI0010_HardcodingAndConfiguration();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +var baseUrl = "https://api.example.com/v1";
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Hardcoded URL"));
    }

    [Fact]
    public async Task GCI0010_HardcodedPort_ShouldFlag()
    {
        var rule = new GCI0010_HardcodingAndConfiguration();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +var conn = "host:8080";
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Hardcoded port"));
    }

    [Fact]
    public async Task GCI0010_HardcodedEnvironmentName_ShouldFlag()
    {
        var rule = new GCI0010_HardcodingAndConfiguration();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +if (env == "production") { Deploy(); }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Hardcoded environment"));
    }

    [Fact]
    public async Task GCI0010_CommentWithIp_ShouldNotFlag()
    {
        var rule = new GCI0010_HardcodingAndConfiguration();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +// Old server was at 192.168.1.100
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("IP address"));
    }

    // ── GCI0012 additional patterns ───────────────────────────────────────────

    [Fact]
    public async Task GCI0012_WeakCrypto_DES_ShouldFlag()
    {
        var rule = new GCI0012_SecurityRisk();
        var raw = """
            diff --git a/src/Crypto.cs b/src/Crypto.cs
            index abc..def 100644
            --- a/src/Crypto.cs
            +++ b/src/Crypto.cs
            @@ -1,1 +1,2 @@
             // crypto
            +var des = new DESCryptoServiceProvider();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("DESCryptoServiceProvider"));
    }

    [Fact]
    public async Task GCI0012_DangerousApi_ProcessStart_ShouldFlag()
    {
        var rule = new GCI0012_SecurityRisk();
        var raw = """
            diff --git a/src/Shell.cs b/src/Shell.cs
            index abc..def 100644
            --- a/src/Shell.cs
            +++ b/src/Shell.cs
            @@ -1,1 +1,2 @@
             // shell
            +Process.Start(userInput);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Process.Start("));
    }

    [Fact]
    public async Task GCI0012_InsecureDeserialization_ShouldFlag()
    {
        var rule = new GCI0012_SecurityRisk();
        var raw = """
            diff --git a/src/Serializer.cs b/src/Serializer.cs
            index abc..def 100644
            --- a/src/Serializer.cs
            +++ b/src/Serializer.cs
            @@ -1,1 +1,2 @@
             // serializer
            +var obj = JsonConvert.DeserializeObject(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TypeNameHandling"));
    }

    [Fact]
    public async Task GCI0012_AllowAnonymousReplacingAuthorize_ShouldFlag()
    {
        var rule = new GCI0012_SecurityRisk();
        var raw = """
            diff --git a/src/UserController.cs b/src/UserController.cs
            index abc..def 100644
            --- a/src/UserController.cs
            +++ b/src/UserController.cs
            @@ -1,3 +1,3 @@
             // controller
            -[Authorize]
            +[AllowAnonymous]
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("[AllowAnonymous]"));
    }

    // ── GCI0016 additional patterns ───────────────────────────────────────────

    [Fact]
    public async Task GCI0016_LockThis_ShouldFlag()
    {
        var rule = new GCI0016_ConcurrencyAndStateRisk();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +lock (this) { DoWork(); }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("lock(this)"));
    }

    [Fact]
    public async Task GCI0016_StaticMutableField_ShouldFlag()
    {
        var rule = new GCI0016_ConcurrencyAndStateRisk();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +private static int _counter;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Static mutable field"));
    }

    // ── RuleOrchestrator exception handling ───────────────────────────────────

    [Fact]
    public async Task RuleOrchestrator_WhenRuleThrows_ShouldContinueAndReturn()
    {
        // A rule that always throws
        var throwingRule = new ThrowingRule();
        var orchestrator = new RuleOrchestrator([throwingRule]);
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

        // Should not throw; returns result with 1 rule evaluated
        Assert.NotNull(result);
        Assert.Equal(1, result.RulesEvaluated);
    }

    private sealed class ThrowingRule : GauntletCI.Core.Rules.IRule
    {
        public string Id => "GCI_TEST";
        public string Name => "Throwing Test Rule";

        public Task<List<GauntletCI.Core.Model.Finding>> EvaluateAsync(
            GauntletCI.Core.Analysis.AnalysisContext context,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Test rule always throws");
    }

    // ── GCI0006 static analysis path ─────────────────────────────────────────

    [Fact]
    public async Task GCI0006_WithStaticAnalysis_CA1062_ShouldAddFinding()
    {
        var rule = new GCI0006_EdgeCaseHandling();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new AnalyzerResult
        {
            Success = true,
            Diagnostics = [new() { Id = "CA1062", Message = "Validate parameter 'input'", FilePath = "src/Foo.cs", Line = 5 }]
        };

        var findings = await rule.EvaluateAsync(diff, staticAnalysis);

        Assert.Contains(findings, f => f.Summary.Contains("CA1062"));
    }

    // ── GCI0007 static analysis path ─────────────────────────────────────────

    [Fact]
    public async Task GCI0007_WithStaticAnalysis_CA1031_ShouldAddFinding()
    {
        var rule = new GCI0007_ErrorHandlingIntegrity();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new AnalyzerResult
        {
            Success = true,
            Diagnostics =
            [
                new() { Id = "CA1031", Message = "Catch specific exception type", FilePath = "src/Foo.cs", Line = 5 },
                new() { Id = "CA2000", Message = "Dispose object", FilePath = "src/Foo.cs", Line = 8 },
                new() { Id = "CA1001", Message = "Types owning disposable", FilePath = "src/Foo.cs", Line = 10 }
            ]
        };

        var findings = await rule.EvaluateAsync(diff, staticAnalysis);

        // GCI0007 owns CA1031 only. CA2000/CA1001 are owned by GCI0024 (Resource Lifecycle)
        // — see DiagnosticMapper. This prevents the same Roslyn diagnostic from producing
        // two findings (one in each rule).
        Assert.Contains(findings, f => f.Summary.Contains("CA1031") && f.RuleId == "GCI0007");
        Assert.DoesNotContain(findings, f => f.Summary.Contains("CA2000") && f.RuleId == "GCI0007");
        Assert.DoesNotContain(findings, f => f.Summary.Contains("CA1001") && f.RuleId == "GCI0007");
    }

    [Fact]
    public async Task GCI0024_WithStaticAnalysis_CA2000_And_CA1001_ShouldAddFindings()
    {
        var rule = new GauntletCI.Core.Rules.Implementations.GCI0024_ResourceLifecycle();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new AnalyzerResult
        {
            Success = true,
            Diagnostics =
            [
                new() { Id = "CA2000", Message = "Dispose object", FilePath = "src/Foo.cs", Line = 8 },
                new() { Id = "CA1001", Message = "Types owning disposable", FilePath = "src/Foo.cs", Line = 10 }
            ]
        };

        var findings = await rule.EvaluateAsync(diff, staticAnalysis);

        Assert.Contains(findings, f => f.Summary.Contains("CA2000") && f.RuleId == "GCI0024");
        Assert.Contains(findings, f => f.Summary.Contains("CA1001") && f.RuleId == "GCI0024");
    }

    // ── GCI0015 static analysis path ─────────────────────────────────────────

    [Fact]
    public async Task GCI0015_WithStaticAnalysis_CA2227_ShouldAddFinding()
    {
        var rule = new GCI0015_DataIntegrityRisk();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new AnalyzerResult
        {
            Success = true,
            Diagnostics = [new() { Id = "CA2227", Message = "Collection properties should be read only", FilePath = "src/Foo.cs", Line = 3 }]
        };

        var findings = await rule.EvaluateAsync(diff, staticAnalysis);

        Assert.Contains(findings, f => f.Summary.Contains("CA2227"));
    }

    // ── GCI0012 static analysis path ─────────────────────────────────────────

    [Fact]
    public async Task GCI0012_WithStaticAnalysis_CA2100_ShouldAddFinding()
    {
        var rule = new GCI0012_SecurityRisk();
        var diff = DiffParser.Parse("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +var x = 1;
            """);
        var staticAnalysis = new AnalyzerResult
        {
            Success = true,
            Diagnostics = [new() { Id = "CA2100", Message = "Review SQL queries for security vulnerabilities", FilePath = "src/Foo.cs", Line = 5 }]
        };

        var findings = await rule.EvaluateAsync(diff, staticAnalysis);

        Assert.Contains(findings, f => f.Summary.Contains("CA2100"));
    }

    // ── GCI0004 public class/interface removed ────────────────────────────────

    [Fact]
    public async Task GCI0004_RemovedPublicClass_ShouldFlag()
    {
        var rule = new GCI0004_BreakingChangeRisk();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,2 @@
             // file
            -public class UserService {
             // end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Public API removed"));
    }

    // ── GCI0003 no logic removed, no finding ──────────────────────────────────

    [Fact]
    public async Task GCI0003_NoLogicRemoved_ShouldNotFlag()
    {
        var rule = new GCI0003_BehavioralChangeDetection();
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    // ── EvaluationResult synthesis ────────────────────────────────────────────

    [Fact]
    public async Task EvaluationResult_HasFindings_WhenEmpty_ReturnsFalse()
    {
        var orchestrator = new RuleOrchestrator([]);
        var diff = DiffParser.Parse("diff --git a/src/Foo.cs b/src/Foo.cs");
        var result = await orchestrator.RunAsync(diff);
        Assert.False(result.HasFindings);
    }
}
