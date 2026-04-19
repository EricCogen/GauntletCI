// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class GitHubChecksWriterTests
{
    private static EvaluationResult MakeResult(params Finding[] findings) =>
        new() { Findings = [.. findings] };

    private static Finding MakeFinding(
        RuleSeverity severity = RuleSeverity.Block,
        string? filePath = "src/Foo.cs",
        int? line = 10,
        string ruleId = "GCI0001") => new()
    {
        RuleId          = ruleId,
        RuleName        = "Test Rule",
        Summary         = "test finding",
        Evidence        = "evidence",
        WhyItMatters    = "why it matters",
        SuggestedAction = "do something",
        Confidence      = Confidence.High,
        Severity        = severity,
        FilePath        = filePath,
        Line            = line,
    };

    // ── BuildConclusion ───────────────────────────────────────────────────────

    [Fact]
    public void BuildConclusion_WithBlockFinding_ReturnsFailure()
    {
        var result = MakeResult(MakeFinding(RuleSeverity.Block));

        Assert.Equal("failure", GitHubChecksWriter.BuildConclusion(result));
    }

    [Fact]
    public void BuildConclusion_WithWarnOnlyFindings_ReturnsNeutral()
    {
        var result = MakeResult(MakeFinding(RuleSeverity.Warn));

        Assert.Equal("neutral", GitHubChecksWriter.BuildConclusion(result));
    }

    [Fact]
    public void BuildConclusion_WithNoFindings_ReturnsSuccess()
    {
        var result = MakeResult();

        Assert.Equal("success", GitHubChecksWriter.BuildConclusion(result));
    }

    [Fact]
    public void BuildConclusion_BlockAndWarn_ReturnsFailure()
    {
        var result = MakeResult(
            MakeFinding(RuleSeverity.Warn),
            MakeFinding(RuleSeverity.Block));

        Assert.Equal("failure", GitHubChecksWriter.BuildConclusion(result));
    }

    // ── BuildAnnotations ──────────────────────────────────────────────────────

    [Fact]
    public void BuildAnnotations_LimitsTo50()
    {
        var findings = Enumerable.Range(1, 60)
            .Select(i => MakeFinding(RuleSeverity.Warn, filePath: $"src/F{i}.cs", line: i, ruleId: $"GCI{i:D4}"))
            .ToArray();

        var result      = MakeResult(findings);
        var annotations = GitHubChecksWriter.BuildAnnotations(result);

        Assert.Equal(50, annotations.Count);
    }

    [Fact]
    public void BuildAnnotations_PrioritizesBlockOverWarn()
    {
        var warnFinding  = MakeFinding(RuleSeverity.Warn,  filePath: "src/Warn.cs",  line: 1, ruleId: "GCI9001");
        var blockFinding = MakeFinding(RuleSeverity.Block, filePath: "src/Block.cs", line: 2, ruleId: "GCI9002");

        var result      = MakeResult(warnFinding, blockFinding);
        var annotations = GitHubChecksWriter.BuildAnnotations(result);

        // Block annotation should appear before Warn annotation
        Assert.Equal(2, annotations.Count);
        var first = annotations[0].ToString()!;
        Assert.Contains("Block.cs", first);
    }

    [Fact]
    public void BuildAnnotations_SkipsFindingsWithoutLocation()
    {
        var withLocation    = MakeFinding(RuleSeverity.Block, filePath: "src/Foo.cs", line: 5);
        var withoutFilePath = MakeFinding(RuleSeverity.Block, filePath: null, line: 5);
        var withoutLine     = MakeFinding(RuleSeverity.Block, filePath: "src/Bar.cs", line: null);

        var result      = MakeResult(withLocation, withoutFilePath, withoutLine);
        var annotations = GitHubChecksWriter.BuildAnnotations(result);

        Assert.Single(annotations);
    }

    [Fact]
    public async Task WriteAsync_MissingEnvVars_SoftFails()
    {
        var prevToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            var result = MakeResult(MakeFinding());

            // Must not throw
            await GitHubChecksWriter.WriteAsync(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", prevToken);
        }
    }
}
