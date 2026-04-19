// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class CoverageCorrelatorTests
{
    private static EvaluationResult MakeResult(params Finding[] findings) =>
        new() { Findings = [.. findings] };

    private static Finding MakeFinding(
        RuleSeverity severity = RuleSeverity.Block,
        string? filePath = "src/MyService.cs") => new()
    {
        RuleId          = "GCI0001",
        RuleName        = "Test Rule",
        Summary         = "test finding",
        Evidence        = "evidence",
        WhyItMatters    = "why",
        SuggestedAction = "action",
        Confidence      = Confidence.High,
        Severity        = severity,
        FilePath        = filePath,
    };

    [Fact]
    public void ParseCoverageResponse_ValidJsonWithFiles_ExtractsFilesCoverage()
    {
        var json = """
            {
              "files": [
                { "name": "src/MyService.cs", "totals": { "coverage": 82.5 } },
                { "name": "src/Other.cs",     "totals": { "coverage": 0.0  } }
              ]
            }
            """;

        var result = CoverageCorrelator.ParseCoverageResponse(json);

        Assert.NotNull(result);
        Assert.Equal(82.5, result!["src/MyService.cs"]);
        Assert.Equal(0.0,  result!["src/Other.cs"]);
    }

    [Fact]
    public void ParseCoverageResponse_NoFilesProperty_ReturnsNull()
    {
        var json = """{ "totals": { "coverage": 75.0 } }""";

        var result = CoverageCorrelator.ParseCoverageResponse(json);

        Assert.Null(result);
    }

    [Fact]
    public void ParseCoverageResponse_InvalidJson_ReturnsNull()
    {
        var result = CoverageCorrelator.ParseCoverageResponse("not-json");

        Assert.Null(result);
    }

    [Fact]
    public async Task AnnotateAsync_MissingEnvVars_SoftFails()
    {
        // Ensure no CODECOV_TOKEN is set so the method exits early without throwing.
        var prev = Environment.GetEnvironmentVariable("CODECOV_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("CODECOV_TOKEN", null);
            var result = MakeResult(MakeFinding());

            // Must not throw
            await CoverageCorrelator.AnnotateAsync(result);

            // Finding should be unchanged
            Assert.Null(result.Findings[0].LlmExplanation);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODECOV_TOKEN", prev);
        }
    }
}
