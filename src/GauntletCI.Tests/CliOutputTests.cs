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

    [Fact]
    public void MaskEvidenceSnippet_MultipleColons_RedactsAfterFirstColonSpace()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet("Line 10: user.Email = foo: bar: baz");
        Assert.Equal("Line 10: [REDACTED]", result);
    }

    [Fact]
    public void MaskEvidenceSnippet_ColonWithoutSpace_ReturnsUnchanged()
    {
        var result = ConsoleReporter.MaskEvidenceSnippet("http://example.com/path");
        Assert.Equal("http://example.com/path", result);
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
        // file= and line= come from structured FilePath/Line fields now
        var f = new Finding
        {
            RuleId = "GCI0001", RuleName = "Diff Integrity",
            Summary = "Something risky", Evidence = "x = secret",
            WhyItMatters = "It matters.", SuggestedAction = "Fix it.",
            Confidence = Confidence.High,
            FilePath = "src/Auth.cs",
            Line = 77,
        };
        var output = CaptureAnnotations(MakeResult(f));
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

    [Fact]
    public void Write_WithExpertContext_IncludesExpertContent()
    {
        var f = MakeFinding();
        f.ExpertContext = new GauntletCI.Core.Model.ExpertFact(
            "IDisposable fields must be disposed.", "https://github.com/dotnet/runtime/issues/358", 0.92f);
        var output = CaptureAnnotations(MakeResult(f));
        Assert.Contains("IDisposable fields must be disposed.", output);
        Assert.Contains("Expert:", output);
    }

    [Fact]
    public void Write_WithExpertContext_IncludesGitHubSourceUrl()
    {
        var f = MakeFinding();
        f.ExpertContext = new GauntletCI.Core.Model.ExpertFact(
            "Some content.", "https://github.com/dotnet/runtime/issues/358", 0.85f);
        var output = CaptureAnnotations(MakeResult(f));
        Assert.Contains("https://github.com/dotnet/runtime/issues/358", output);
    }

    [Fact]
    public void Write_WithoutExpertContext_NoExpertPrefix()
    {
        var output = CaptureAnnotations(MakeResult(MakeFinding()));
        Assert.DoesNotContain("Expert:", output);
    }

    [Fact]
    public void Write_WithLlmExplanation_IncludesLlmLabel()
    {
        var f = MakeFinding();
        f.LlmExplanation = "This pattern causes socket exhaustion.";
        var output = CaptureAnnotations(MakeResult(f));
        Assert.Contains("LLM:", output);
        Assert.Contains("socket exhaustion", output);
    }

    [Fact]
    public void Write_WithBothLlmAndExpertContext_IncludesBoth()
    {
        var f = MakeFinding();
        f.LlmExplanation = "Async deadlock risk here.";
        f.ExpertContext  = new GauntletCI.Core.Model.ExpertFact(
            "SemaphoreSlim preferred over lock.", "https://github.com/dotnet/runtime/issues/22144", 0.78f);
        var output = CaptureAnnotations(MakeResult(f));
        Assert.Contains("LLM:", output);
        Assert.Contains("Expert:", output);
        Assert.Contains("Async deadlock", output);
        Assert.Contains("SemaphoreSlim", output);
    }

    [Fact]
    public void BuildMessage_NoExtras_ReturnsSummaryOnly()
    {
        var f = MakeFinding(summary: "Plain summary");
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.Equal("Plain summary", msg);
    }

    [Fact]
    public void BuildMessage_ExpertContextNewlines_AreEscaped()
    {
        var f = MakeFinding();
        f.ExpertContext = new GauntletCI.Core.Model.ExpertFact(
            "Line one\nLine two.", "https://github.com/dotnet/runtime/issues/1", 0.5f);
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.DoesNotContain("\n", msg);
        Assert.Contains("%0A", msg);
    }

    [Fact]
    public void BuildMessage_NullLlmExplanation_DoesNotThrow()
    {
        var f = MakeFinding();
        f.LlmExplanation = null;
        var ex = Record.Exception(() => GitHubAnnotationWriter.BuildMessage(f));
        Assert.Null(ex);
    }

    [Fact]
    public void BuildMessage_EmptyLlmExplanation_DoesNotIncludeLlmLabel()
    {
        var f = MakeFinding();
        f.LlmExplanation = "";
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.DoesNotContain("LLM:", msg);
    }

    [Fact]
    public void BuildMessage_WhitespaceLlmExplanation_DoesNotIncludeLlmLabel()
    {
        var f = MakeFinding();
        f.LlmExplanation = "   ";
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.DoesNotContain("LLM:", msg);
    }

    [Fact]
    public void Write_NullFilePath_OmitsFileParameter()
    {
        var f = MakeFinding();
        f.FilePath = null;
        f.Line = 10;
        var output = CaptureAnnotations(MakeResult(f));
        Assert.DoesNotContain("file=", output);
    }

    [Fact]
    public void Write_EmptyFilePath_OmitsFileParameter()
    {
        var f = new Finding
        {
            RuleId = "GCI0001", RuleName = "Diff Integrity",
            Summary = "Something risky", Evidence = "x = secret",
            WhyItMatters = "It matters.", SuggestedAction = "Fix it.",
            Confidence = Confidence.High,
            FilePath = "",
            Line = 77,
        };
        var output = CaptureAnnotations(MakeResult(f));
        Assert.DoesNotContain("file=,", output);
    }

    [Fact]
    public void Write_NullLine_DefaultsToLineOne()
    {
        var f = new Finding
        {
            RuleId = "GCI0001", RuleName = "Diff Integrity",
            Summary = "Something risky", Evidence = "x = secret",
            WhyItMatters = "It matters.", SuggestedAction = "Fix it.",
            Confidence = Confidence.High,
            FilePath = "src/Test.cs",
            Line = null,
        };
        var output = CaptureAnnotations(MakeResult(f));
        Assert.Contains("line=1", output);
    }
}
