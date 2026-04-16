// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;

namespace GauntletCI.Tests.Corpus;

public class LlmLabelerHelpersTests
{
    // ── BuildPrompt ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_ContainsRuleId()
    {
        var prompt = LlmLabelerHelpers.BuildPrompt("GCI0001", "finding", "evidence", null, "", "");
        Assert.Contains("GCI0001", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsFilePath()
    {
        var prompt = LlmLabelerHelpers.BuildPrompt("GCI0001", "finding", "evidence", "src/Foo.cs", "", "");
        Assert.Contains("src/Foo.cs", prompt);
    }

    [Fact]
    public void BuildPrompt_NullFilePath_ContainsUnknown()
    {
        var prompt = LlmLabelerHelpers.BuildPrompt("GCI0001", "finding", "evidence", null, "", "");
        Assert.Contains("unknown", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsFindingMessage()
    {
        var prompt = LlmLabelerHelpers.BuildPrompt("GCI0001", "some message", "evidence", null, "", "");
        Assert.Contains("some message", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsJsonInstructions()
    {
        var prompt = LlmLabelerHelpers.BuildPrompt("GCI0001", "finding", "evidence", null, "", "");
        Assert.Contains("should_trigger", prompt);
    }

    // ── ParseJson ─────────────────────────────────────────────────────────────

    [Fact]
    public void ParseJson_ValidJson_ReturnsParsedResult()
    {
        var result = LlmLabelerHelpers.ParseJson("""{"should_trigger": true, "confidence": 0.9, "reason": "clear bug"}""");
        Assert.NotNull(result);
        Assert.True(result!.ShouldTrigger);
        Assert.Equal(0.9, result.Confidence);
        Assert.Equal("clear bug", result.Reason);
        Assert.False(result.IsInconclusive);
    }

    [Fact]
    public void ParseJson_LowConfidence_IsInconclusive()
    {
        var result = LlmLabelerHelpers.ParseJson("""{"should_trigger": false, "confidence": 0.3, "reason": "uncertain"}""");
        Assert.NotNull(result);
        Assert.True(result!.IsInconclusive);
    }

    [Fact]
    public void ParseJson_WithMarkdownFences_StripsFences()
    {
        var json = """{"should_trigger": true, "confidence": 0.9, "reason": "clear bug"}""";
        var fenced = "```\n" + json + "\n```";
        var result = LlmLabelerHelpers.ParseJson(fenced);
        Assert.NotNull(result);
        Assert.True(result!.ShouldTrigger);
    }

    [Fact]
    public void ParseJson_MissingField_ReturnsNull()
    {
        // Missing "confidence" and "reason"
        var result = LlmLabelerHelpers.ParseJson("""{"should_trigger": true}""");
        Assert.Null(result);
    }

    [Fact]
    public void ParseJson_InvalidJson_ReturnsNull()
    {
        Assert.Null(LlmLabelerHelpers.ParseJson("not json"));
    }

    [Fact]
    public void ParseJson_EmptyString_ReturnsNull()
    {
        Assert.Null(LlmLabelerHelpers.ParseJson(""));
    }

    // ── TruncateComments ──────────────────────────────────────────────────────

    [Fact]
    public void TruncateComments_ShortComments_ReturnsJoined()
    {
        var result = LlmLabelerHelpers.TruncateComments(["hello", "world"]);
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void TruncateComments_ExceedsMax_TruncatesAtMaxChars()
    {
        var longComment = new string('a', 600);
        var result = LlmLabelerHelpers.TruncateComments([longComment], maxChars: 500);
        Assert.Equal(500, result.Length);
    }

    [Fact]
    public void TruncateComments_ExactlyAtMax_ReturnsUnchanged()
    {
        var exactComment = new string('b', 500);
        var result = LlmLabelerHelpers.TruncateComments([exactComment], maxChars: 500);
        Assert.Equal(500, result.Length);
    }

    // ── TruncateDiff ──────────────────────────────────────────────────────────

    [Fact]
    public void TruncateDiff_ShortDiff_ReturnsUnchanged()
    {
        var result = LlmLabelerHelpers.TruncateDiff("small diff");
        Assert.Equal("small diff", result);
    }

    [Fact]
    public void TruncateDiff_ExceedsMax_TruncatesAtMaxChars()
    {
        var longDiff = new string('x', 900);
        var result = LlmLabelerHelpers.TruncateDiff(longDiff, maxChars: 800);
        Assert.Equal(800, result.Length);
    }
}
