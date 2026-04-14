// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.Mcp;

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
}
