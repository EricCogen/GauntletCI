// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Tests;

public class TargetFrameworkDetectorTests
{
    [Theory]
    [InlineData("net8.0",  true)]
    [InlineData("net9.0",  true)]
    [InlineData("net10.0", true)]
    [InlineData("net6.0",  false)]
    [InlineData("net7.0",  false)]
    [InlineData("netstandard2.0", false)]
    [InlineData("netcoreapp3.1", false)]
    [InlineData("net48",  false)]
    [InlineData(null,     false)]
    [InlineData("",       false)]
    public void IsNet8OrLater_ReturnsExpected(string? tfm, bool expected)
        => Assert.Equal(expected, TargetFrameworkDetector.IsNet8OrLater(tfm));

    [Fact]
    public void Detect_NullOrEmptyPath_ReturnsNull()
    {
        Assert.Null(TargetFrameworkDetector.Detect(null!));
        Assert.Null(TargetFrameworkDetector.Detect(""));
    }

    [Fact]
    public void Detect_MissingDirectory_ReturnsNull()
        => Assert.Null(TargetFrameworkDetector.Detect(@"C:\this\path\does\not\exist"));

    [Fact]
    public void Detect_CsprojWithTargetFramework_ReturnsValue()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "App.csproj"),
                "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

            Assert.Equal("net8.0", TargetFrameworkDetector.Detect(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Detect_CsprojWithTargetFrameworks_ReturnsPrimary()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Lib.csproj"),
                "<Project><PropertyGroup><TargetFrameworks>net8.0;net6.0</TargetFrameworks></PropertyGroup></Project>");

            Assert.Equal("net8.0", TargetFrameworkDetector.Detect(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Detect_NoCsproj_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "README.md"), "# hello");
            Assert.Null(TargetFrameworkDetector.Detect(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

public class SarifWriterTests
{
    private static EvaluationResult BuildResult(params Finding[] findings) =>
        new()
        {
            CommitSha = "abc1234",
            Findings = [.. findings],
            RulesEvaluated = 1,
        };

    [Fact]
    public void Serialize_EmptyFindings_ValidSarifSchema()
    {
        var result = BuildResult();
        var json = SarifWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
        Assert.True(doc.RootElement.TryGetProperty("$schema", out _));
        var runs = doc.RootElement.GetProperty("runs");
        Assert.Equal(JsonValueKind.Array, runs.ValueKind);
        Assert.Equal(0, runs[0].GetProperty("results").GetArrayLength());
    }

    [Fact]
    public void Serialize_BlockFinding_LevelIsError()
    {
        var finding = new Finding
        {
            RuleId = "GCI0048",
            RuleName = "Insecure Random in Security Context",
            Summary = "System.Random used near token",
            Evidence = "Line 5: var r = new Random();",
            WhyItMatters = "Predictable output",
            SuggestedAction = "Use RandomNumberGenerator",
            Confidence = Confidence.High,
            Severity = RuleSeverity.Block,
            FilePath = "src/Auth.cs",
            Line = 5,
        };

        var json = SarifWriter.Serialize(BuildResult(finding));
        var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.Equal("GCI0048", result.GetProperty("ruleId").GetString());
        Assert.Equal("error", result.GetProperty("level").GetString());

        var loc = result.GetProperty("locations")[0]
            .GetProperty("physicalLocation");
        Assert.Equal("src/Auth.cs", loc.GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.Equal(5, loc.GetProperty("region").GetProperty("startLine").GetInt32());
    }

    [Fact]
    public void Serialize_WarnFinding_LevelIsWarning()
    {
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Diff Integrity",
            Summary = "Test",
            Evidence = "Line 1: foo",
            WhyItMatters = "Why",
            SuggestedAction = "Action",
            Confidence = Confidence.Medium,
            Severity = RuleSeverity.Warn,
        };

        var json = SarifWriter.Serialize(BuildResult(finding));
        var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.Equal("warning", result.GetProperty("level").GetString());
    }

    [Fact]
    public void Serialize_RulesDeduplicatedInDriver()
    {
        var f1 = new Finding
        {
            RuleId = "GCI0001", RuleName = "Rule One", Summary = "s", Evidence = "e",
            WhyItMatters = "w", SuggestedAction = "a", Confidence = Confidence.Low, Severity = RuleSeverity.Info,
        };
        var f2 = new Finding
        {
            RuleId = "GCI0001", RuleName = "Rule One", Summary = "s2", Evidence = "e2",
            WhyItMatters = "w2", SuggestedAction = "a2", Confidence = Confidence.Low, Severity = RuleSeverity.Info,
        };

        var json = SarifWriter.Serialize(BuildResult(f1, f2));
        var doc = JsonDocument.Parse(json);
        var rules = doc.RootElement.GetProperty("runs")[0]
            .GetProperty("tool").GetProperty("driver").GetProperty("rules");

        Assert.Equal(1, rules.GetArrayLength());
    }

    [Fact]
    public void Serialize_EnrichedFinding_IncludesPropertiesInSarif()
    {
        var finding = new Finding
        {
            RuleId = "GCI0048",
            RuleName = "Insecure Random",
            Summary = "System.Random used",
            Evidence = "Line 5: var r = new Random();",
            WhyItMatters = "Predictable output",
            SuggestedAction = "Use RandomNumberGenerator",
            Confidence = Confidence.High,
            Severity = RuleSeverity.Block,
            FilePath = "src/Auth.cs",
            Line = 5,
            CodeSnippet = "var r = new Random();\r\nvar num = r.Next();",
            LlmExplanation = "This uses Random which is predictable. Use RandomNumberGenerator.GetInt32() instead.",
            ExpertContext = new ExpertFact(
                Content: "System.Random is cryptographically insecure",
                Source: "OWASP A06:2021 - Cryptographic Failures",
                Score: 0.95f
            ),
        };

        var json = SarifWriter.Serialize(BuildResult(finding));
        var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        // Verify base properties are present
        Assert.Equal("GCI0048", result.GetProperty("ruleId").GetString());
        Assert.Equal("error", result.GetProperty("level").GetString());

        // Verify enrichment properties are included
        Assert.True(result.TryGetProperty("properties", out var props));
        Assert.Equal("var r = new Random();\r\nvar num = r.Next();", 
            props.GetProperty("codeSnippet").GetString());
        Assert.Equal("This uses Random which is predictable. Use RandomNumberGenerator.GetInt32() instead.",
            props.GetProperty("llmExplanation").GetString());
        Assert.Equal("System.Random is cryptographically insecure",
            props.GetProperty("expertContextContent").GetString());
        Assert.Equal("OWASP A06:2021 - Cryptographic Failures",
            props.GetProperty("expertContextSource").GetString());
        Assert.True(props.GetProperty("expertContextScore").GetSingle() > 0.9f);
    }

    [Fact]
    public void Serialize_FindingWithoutEnrichment_NoPropertiesInSarif()
    {
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test Rule",
            Summary = "Test",
            Evidence = "Line 1: foo",
            WhyItMatters = "Why",
            SuggestedAction = "Action",
            Confidence = Confidence.Low,
            Severity = RuleSeverity.Info,
            FilePath = "test.cs",
            Line = 1,
        };

        var json = SarifWriter.Serialize(BuildResult(finding));
        var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        // Should not have properties key if no enrichment
        Assert.False(result.TryGetProperty("properties", out _));
    }
}
