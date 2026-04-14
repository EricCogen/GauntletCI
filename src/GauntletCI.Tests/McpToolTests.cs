// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.Mcp;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Tests;

public class McpToolTests
{
    private const string SampleDiff = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index 0000000..1111111 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,5 +1,8 @@
         public class Foo
         {
        -    public void Bar() { }
        +    public void Bar()
        +    {
        +        Console.WriteLine("hello");
        +    }
         }
        """;

    private const string CredentialDiff = """
        diff --git a/src/Config.cs b/src/Config.cs
        index 0000000..1111111 100644
        --- a/src/Config.cs
        +++ b/src/Config.cs
        @@ -1,3 +1,4 @@
         public class Config
         {
        +    public string Password = "hunter2";
         }
        """;

    [Fact]
    public async Task analyze_diff_EmptyDiff_ReturnsNoFindings()
    {
        var result = await GauntletTools.analyze_diff("");
        var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("hasFindings").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("findingCount").GetInt32());
    }

    [Fact]
    public async Task analyze_diff_ValidDiff_ReturnsJsonString()
    {
        var result = await GauntletTools.analyze_diff(SampleDiff);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("hasFindings", out _));
        Assert.True(doc.RootElement.TryGetProperty("findingCount", out _));
    }

    [Fact]
    public async Task analyze_diff_InvalidInput_ReturnsValidJson()
    {
        var result = await GauntletTools.analyze_diff("not a valid diff at all $$##@@");

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void list_rules_ReturnsNonEmptyList()
    {
        var result = GauntletTools.list_rules();

        Assert.NotNull(result);
        var arr = JsonDocument.Parse(result).RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.True(arr.GetArrayLength() > 0);

        foreach (var item in arr.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("id", out _));
            Assert.True(item.TryGetProperty("name", out _));
        }
    }

    [Fact]
    public void list_rules_AllHaveRuleIdFormat()
    {
        var result = GauntletTools.list_rules();
        var arr = JsonDocument.Parse(result).RootElement;

        foreach (var item in arr.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            Assert.Matches(@"^GCI\d{4}$", id);
        }
    }

    [Fact]
    public async Task audit_stats_ReturnsValidJson()
    {
        var result = await GauntletTools.audit_stats();

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("totalScans", out _));
    }

    [Fact]
    public async Task analyze_staged_InvalidRepo_ReturnsErrorJson()
    {
        var result = await GauntletTools.analyze_staged(@"C:\does-not-exist");

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task analyze_diff_WithNullEngine_FindingsHaveNoLlmExplanation()
    {
        GauntletTools.SetEngine(new NullLlmEngine());

        var result = await GauntletTools.analyze_diff(CredentialDiff);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        Assert.True(findings.Count > 0);
        foreach (var f in findings)
        {
            // NullLlmEngine produces no enrichment — llmExplanation should be absent or null
            if (f.TryGetProperty("llmExplanation", out var expl))
                Assert.Equal(JsonValueKind.Null, expl.ValueKind);
        }
    }

    [Fact]
    public async Task analyze_diff_WithFakeEngine_HighFindingsGetLlmExplanation()
    {
        GauntletTools.SetEngine(new FakeLlmEngine("enriched by ollama"));

        var result = await GauntletTools.analyze_diff(CredentialDiff);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        var highFindings = findings
            .Where(f => f.GetProperty("confidence").GetString() == "High")
            .ToList();

        Assert.True(highFindings.Count > 0, "Expected at least one High finding from credential diff");
        Assert.All(highFindings, f =>
        {
            Assert.True(f.TryGetProperty("llmExplanation", out var expl));
            Assert.Equal("enriched by ollama", expl.GetString());
        });
    }

    [Fact]
    public async Task analyze_diff_WithFakeEngine_LimitedToThreeEnrichments()
    {
        // Build a diff that triggers many High findings
        var sb = new System.Text.StringBuilder();
        for (var i = 1; i <= 5; i++)
        {
            sb.AppendLine($"diff --git a/src/Config{i}.cs b/src/Config{i}.cs");
            sb.AppendLine($"index 0000000..111111{i} 100644");
            sb.AppendLine($"--- a/src/Config{i}.cs");
            sb.AppendLine($"+++ b/src/Config{i}.cs");
            sb.AppendLine("@@ -1,3 +1,4 @@");
            sb.AppendLine($" public class Config{i}");
            sb.AppendLine(" {");
            sb.AppendLine($"+    public string Password = \"secret{i}\";");
            sb.AppendLine(" }");
        }

        var countingEngine = new CountingLlmEngine();
        GauntletTools.SetEngine(countingEngine);

        await GauntletTools.analyze_diff(sb.ToString());

        Assert.True(countingEngine.CallCount <= 3, $"Expected ≤3 enrichment calls, got {countingEngine.CallCount}");
    }

    [Fact]
    public async Task analyze_diff_WithFakeEngine_NoHighFindings_ZeroEnrichmentCalls()
    {
        // Empty diff produces zero findings → enrichment must not be called
        var countingEngine = new CountingLlmEngine();
        GauntletTools.SetEngine(countingEngine);

        await GauntletTools.analyze_diff("");

        Assert.Equal(0, countingEngine.CallCount);
    }

    [Fact]
    public async Task analyze_diff_WithFakeEngine_OnlyMediumLowFindings_ZeroEnrichmentCalls()
    {
        // SampleDiff adds a Console.WriteLine — triggers complexity/edge-case rules at
        // Medium/Low confidence, but no High-confidence findings.
        var countingEngine = new CountingLlmEngine();
        GauntletTools.SetEngine(countingEngine);

        var result = await GauntletTools.analyze_diff(SampleDiff);
        var doc = JsonDocument.Parse(result);
        var highCount = doc.RootElement.GetProperty("findings").EnumerateArray()
            .Count(f => f.GetProperty("confidence").GetString() == "High");

        // If the diff produces zero High findings, enrichment should not be invoked
        if (highCount == 0)
            Assert.Equal(0, countingEngine.CallCount);
    }

    [Fact]
    public async Task analyze_diff_WhenEngineReturnsEmpty_LlmExplanationAbsentFromJson()
    {
        // An engine that returns empty string → WhenWritingNull suppresses the field
        GauntletTools.SetEngine(new FakeLlmEngine(string.Empty));

        var result = await GauntletTools.analyze_diff(CredentialDiff);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        // llmExplanation should be absent or null when the engine returned ""
        // (empty string is stored as null equivalent by Finding; serializer omits null)
        Assert.True(findings.Count > 0);
        foreach (var f in findings.Where(f => f.GetProperty("confidence").GetString() == "High"))
        {
            var hasExplanation = f.TryGetProperty("llmExplanation", out var expl)
                && expl.ValueKind != JsonValueKind.Null
                && !string.IsNullOrEmpty(expl.GetString());
            Assert.False(hasExplanation, "Expected no llmExplanation when engine returns empty string");
        }
    }

    [Fact]
    public async Task SetEngine_ReplacedEngine_NewEngineIsUsed()
    {
        GauntletTools.SetEngine(new FakeLlmEngine("first"));
        GauntletTools.SetEngine(new FakeLlmEngine("second"));

        var result = await GauntletTools.analyze_diff(CredentialDiff);
        var doc = JsonDocument.Parse(result);
        var highFindings = doc.RootElement.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("confidence").GetString() == "High")
            .ToList();

        Assert.True(highFindings.Count > 0);
        Assert.All(highFindings, f =>
        {
            f.TryGetProperty("llmExplanation", out var expl);
            Assert.Equal("second", expl.GetString());
        });
    }

    // Test doubles

    private sealed class FakeLlmEngine(string response) : ILlmEngine
    {
        public bool IsAvailable => true;
        public Task<string> EnrichFindingAsync(Finding f, CancellationToken ct = default)
            => Task.FromResult(response);
        public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
            => Task.FromResult(response);
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(response);
        public void Dispose() { }
    }

    private sealed class CountingLlmEngine : ILlmEngine
    {
        public int CallCount;
        public bool IsAvailable => true;
        public Task<string> EnrichFindingAsync(Finding f, CancellationToken ct = default)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult("ok");
        }
        public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
            => Task.FromResult("ok");
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult("ok");
        public void Dispose() { }
    }
}

