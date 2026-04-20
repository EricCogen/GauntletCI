// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.IncidentCorrelation;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class TraceCommandTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Finding MakeFinding(
        string ruleId    = "GCI0001",
        string summary   = "A test finding",
        string? filePath = "src/Auth.cs",
        RuleSeverity severity = RuleSeverity.Warn) => new()
    {
        RuleId          = ruleId,
        RuleName        = "Test Rule",
        Summary         = summary,
        Evidence        = "Line 10: some code",
        WhyItMatters    = "risk reason",
        SuggestedAction = "fix it",
        Confidence      = Confidence.High,
        Severity        = severity,
        FilePath        = filePath,
        Line            = 10,
    };

    private static IncidentSummary MakeIncident(
        string id,
        string title,
        string? description = null,
        string source = "PagerDuty") =>
        new(id, title, description, source);

    // ── ParseSince ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseSince_24h_Returns24HoursAgo()
    {
        var now    = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var result = IncidentClient.ParseSince("24h", now);

        Assert.Equal(now.AddHours(-24), result);
    }

    [Fact]
    public void ParseSince_7d_Returns7DaysAgo()
    {
        var now    = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var result = IncidentClient.ParseSince("7d", now);

        Assert.Equal(now.AddDays(-7), result);
    }

    [Fact]
    public void ParseSince_30m_Returns30MinutesAgo()
    {
        var now    = new DateTimeOffset(2024, 1, 1, 6, 0, 0, TimeSpan.Zero);
        var result = IncidentClient.ParseSince("30m", now);

        Assert.Equal(now.AddMinutes(-30), result);
    }

    [Fact]
    public void ParseSince_Invalid_FallsBackTo24h()
    {
        var now = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(now.AddHours(-24), IncidentClient.ParseSince("xyz", now));
        Assert.Equal(now.AddHours(-24), IncidentClient.ParseSince("",    now));
        Assert.Equal(now.AddHours(-24), IncidentClient.ParseSince("-5h", now));
        Assert.Equal(now.AddHours(-24), IncidentClient.ParseSince("h",   now));
    }

    [Fact]
    public void ParseSince_1w_Returns7DaysAgo()
    {
        var now    = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var result = IncidentClient.ParseSince("1w", now);

        Assert.Equal(now.AddDays(-7), result);
    }

    // ── CorrelateIncidents ────────────────────────────────────────────────────

    [Fact]
    public void CorrelateIncidents_FileMatchesTitle_ReturnsCorrelated()
    {
        var finding  = MakeFinding(filePath: "src/Auth.cs");
        var incident = MakeIncident("INC001", "Outage in Auth.cs module — users cannot log in");

        var result = IncidentClient.CorrelateIncidents([finding], [incident]);

        Assert.True(result.ContainsKey("src/Auth.cs"));
        var correlated = result["src/Auth.cs"];
        Assert.Single(correlated);
        Assert.Equal("INC001", correlated[0].Id);
    }

    [Fact]
    public void CorrelateIncidents_FileMatchesDescription_ReturnsCorrelated()
    {
        var finding  = MakeFinding(filePath: "src/PaymentService.cs");
        var incident = MakeIncident("INC002", "Payment failures", "Errors in PaymentService.cs at line 42");

        var result = IncidentClient.CorrelateIncidents([finding], [incident]);

        Assert.True(result.ContainsKey("src/PaymentService.cs"));
        Assert.Single(result["src/PaymentService.cs"]);
    }

    [Fact]
    public void CorrelateIncidents_NoMatch_ReturnsEmpty()
    {
        var finding  = MakeFinding(filePath: "src/Auth.cs");
        var incident = MakeIncident("INC003", "Database connection pool exhausted");

        var result = IncidentClient.CorrelateIncidents([finding], [incident]);

        Assert.True(result.ContainsKey("src/Auth.cs"));
        Assert.Empty(result["src/Auth.cs"]);
    }

    [Fact]
    public void CorrelateIncidents_NoFilePath_IsSkipped()
    {
        var finding = MakeFinding(filePath: null);
        var incident = MakeIncident("INC004", "Some incident");

        var result = IncidentClient.CorrelateIncidents([finding], [incident]);

        Assert.Empty(result);
    }

    [Fact]
    public void CorrelateIncidents_MultipleFindings_GroupsByFile()
    {
        var finding1 = MakeFinding(ruleId: "GCI0001", filePath: "src/Auth.cs");
        var finding2 = MakeFinding(ruleId: "GCI0002", filePath: "src/Auth.cs");
        var finding3 = MakeFinding(ruleId: "GCI0003", filePath: "src/Other.cs");
        var incident = MakeIncident("INC005", "Auth.cs regression");

        var result = IncidentClient.CorrelateIncidents([finding1, finding2, finding3], [incident]);

        Assert.Equal(2, result.Count);
        Assert.Single(result["src/Auth.cs"]);
        Assert.Empty(result["src/Other.cs"]);
    }

    // ── BuildHeatmapJson ──────────────────────────────────────────────────────

    [Fact]
    public void BuildHeatmapJson_WithFindings_ContainsExpectedFields()
    {
        var finding  = MakeFinding(ruleId: "GCI0010", filePath: "src/Auth.cs", severity: RuleSeverity.Block);
        var incident = MakeIncident("INC010", "Auth.cs is broken");
        var correlations = IncidentClient.CorrelateIncidents([finding], [incident]);

        var now   = DateTimeOffset.UtcNow;
        var since = now.AddHours(-24);

        var json = IncidentClient.BuildHeatmapJson(
            "v1.2.3", since, now,
            [finding],
            correlations,
            [incident]);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("v1.2.3", root.GetProperty("baseRef").GetString());
        Assert.True(root.TryGetProperty("since", out _));
        Assert.True(root.TryGetProperty("until", out _));

        var files = root.GetProperty("files");
        Assert.Equal(1, files.GetArrayLength());

        var firstFile = files[0];
        Assert.Equal("src/Auth.cs", firstFile.GetProperty("file").GetString());
        Assert.Equal("Block", firstFile.GetProperty("maxSeverity").GetString());

        var findings = firstFile.GetProperty("findings");
        Assert.Equal(1, findings.GetArrayLength());
        Assert.Equal("GCI0010", findings[0].GetProperty("ruleId").GetString());

        var correlatedInc = firstFile.GetProperty("correlatedIncidents");
        Assert.Equal(1, correlatedInc.GetArrayLength());
        Assert.Equal("INC010", correlatedInc[0].GetProperty("id").GetString());

        var allInc = root.GetProperty("allIncidents");
        Assert.Equal(1, allInc.GetArrayLength());
    }

    [Fact]
    public void BuildHeatmapJson_NoIncidents_EmptyArrays()
    {
        var finding = MakeFinding(filePath: "src/Foo.cs");
        var correlations = IncidentClient.CorrelateIncidents([finding], []);

        var now   = DateTimeOffset.UtcNow;
        var since = now.AddDays(-7);

        var json = IncidentClient.BuildHeatmapJson(
            "abc1234", since, now,
            [finding],
            correlations,
            []);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("allIncidents").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("files")[0]
            .GetProperty("correlatedIncidents").GetArrayLength());
    }
}
