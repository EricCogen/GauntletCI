// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class ConsoleReporterTests
{
    [Fact]
    public void MaskEvidenceSnippet_WithSnippet_RedactsAfterColon()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet("Line 42: _logger.Log(user.Email)");
        Assert.Equal("Line 42: [REDACTED]", result);
    }

    [Fact]
    public void MaskEvidenceSnippet_NoColon_ReturnsUnchanged()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet("src/Auth.cs:42");
        Assert.Equal("src/Auth.cs:42", result);
    }

    [Fact]
    public void MaskEvidenceSnippet_Empty_ReturnsEmpty()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void MaskEvidenceSnippet_PreservesFileAndLine()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet("src/Service.cs:99: secretToken = \"abc\"");
        Assert.StartsWith("src/Service.cs:99: ", result);
        Assert.EndsWith("[REDACTED]", result);
        Assert.DoesNotContain("secretToken", result);
    }
}

public class GitHubAnnotationWriterTests
{
    private static EvaluationResult MakeResult(params Finding[] findings) =>
        new() { Findings = [.. findings], RulesEvaluated = 1 };

    private static Finding MakeFinding(
        string ruleId = "GCI0001",
        string ruleName = "Diff Integrity",
        string summary = "Something risky",
        string evidence = "Line 10: bad code",
        Confidence confidence = Confidence.High) =>
        new()
        {
            RuleId = ruleId, RuleName = ruleName,
            Summary = summary, Evidence = evidence,
            WhyItMatters = "It matters.", SuggestedAction = "Fix it.",
            Confidence = confidence,
        };

    private static string CaptureAnnotations(EvaluationResult result)
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try { GitHubAnnotationWriter.Write(result); }
        finally { Console.SetOut(original); }
        return sw.ToString();
    }

    [Fact]
    public void Write_HighConfidence_EmitsErrorLevel()
    {
        var output = CaptureAnnotations(MakeResult(MakeFinding(confidence: Confidence.High)));
        Assert.Contains("::error", output);
    }

    [Fact]
    public void Write_MediumConfidence_EmitsWarningLevel()
    {
        var output = CaptureAnnotations(MakeResult(MakeFinding(confidence: Confidence.Medium)));
        Assert.Contains("::warning", output);
    }

    [Fact]
    public void Write_LowConfidence_EmitsNoticeLevel()
    {
        var output = CaptureAnnotations(MakeResult(MakeFinding(confidence: Confidence.Low)));
        Assert.Contains("::notice", output);
    }

    [Fact]
    public void Write_IncludesRuleIdInTitle()
    {
        var output = CaptureAnnotations(MakeResult(MakeFinding(ruleId: "GCI0042")));
        Assert.Contains("GCI0042", output);
    }

    [Fact]
    public void Write_EvidenceWithLineNumber_ExtractsLine()
    {
        // file= must be present for line= to appear in the annotation
        var output = CaptureAnnotations(MakeResult(MakeFinding(evidence: "src/Auth.cs Line 77: x = secret")));
        Assert.Contains("line=77", output);
        Assert.Contains("file=src/Auth.cs", output);
    }

    [Fact]
    public void Write_NoFindings_ProducesNoOutput()
    {
        var output = CaptureAnnotations(MakeResult());
        Assert.Equal(string.Empty, output.Trim());
    }

    [Fact]
    public void Write_SummaryNewlines_AreEscaped()
    {
        var f = MakeFinding(summary: "line one\nline two");
        var output = CaptureAnnotations(MakeResult(f));
        Assert.DoesNotContain("\n", output.Split("::error").Last().Split("::").First());
        Assert.Contains("%0A", output);
    }
}
