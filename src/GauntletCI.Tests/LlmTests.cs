// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Tests;

public class NullLlmEngineTests
{
    private readonly NullLlmEngine _engine = new();

    [Fact]
    public void IsAvailable_ReturnsFalse()
    {
        Assert.False(_engine.IsAvailable);
    }

    [Fact]
    public async Task EnrichFindingAsync_ReturnsEmpty()
    {
        var finding = new Finding
        {
            RuleId = "GCI0001", RuleName = "Diff Integrity",
            Summary = "Mixed concerns", Evidence = "Line 1: x",
            WhyItMatters = "Risk.", SuggestedAction = "Fix.",
        };
        var result = await _engine.EnrichFindingAsync(finding);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SummarizeReportAsync_ReturnsEmpty()
    {
        var result = await _engine.SummarizeReportAsync([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var engine = new NullLlmEngine();
        var ex = Record.Exception(() => engine.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ImplementsILlmEngine()
    {
        Assert.IsAssignableFrom<ILlmEngine>(_engine);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsEmpty()
    {
        var result = await _engine.CompleteAsync("any prompt");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task CompleteAsync_WithCancellationToken_ReturnsEmpty()
    {
        using var cts = new CancellationTokenSource();
        var result = await _engine.CompleteAsync("any prompt", cts.Token);
        Assert.Equal(string.Empty, result);
    }
}

public class PromptTemplatesTests
{
    [Fact]
    public void EnrichFinding_ContainsRuleId()
    {
        var prompt = PromptTemplates.EnrichFinding("GCI0007", "Error Handling", "Log removed", "Line 5: x");
        Assert.Contains("GCI0007", prompt);
    }

    [Fact]
    public void EnrichFinding_ContainsRuleName()
    {
        var prompt = PromptTemplates.EnrichFinding("GCI0007", "Error Handling Integrity", "Log removed", "Line 5: x");
        Assert.Contains("Error Handling Integrity", prompt);
    }

    [Fact]
    public void EnrichFinding_ContainsSummaryAndEvidence()
    {
        var prompt = PromptTemplates.EnrichFinding("GCI0001", "Diff Integrity", "Mixed concerns", "Line 42: x");
        Assert.Contains("Mixed concerns", prompt);
        Assert.Contains("Line 42: x", prompt);
    }

    [Fact]
    public void EnrichFinding_StartsWithUserToken()
    {
        var prompt = PromptTemplates.EnrichFinding("GCI0001", "Test", "Summary", "Evidence");
        Assert.StartsWith("<|user|>", prompt);
    }

    [Fact]
    public void EnrichFinding_EndsWithAssistantToken()
    {
        var prompt = PromptTemplates.EnrichFinding("GCI0001", "Test", "Summary", "Evidence");
        Assert.EndsWith("<|assistant|>\n", prompt);
    }

    [Fact]
    public void SummarizeReport_NumbersFindings()
    {
        var prompt = PromptTemplates.SummarizeReport(["Missing tests", "Log removed"]);
        Assert.Contains("1.", prompt);
        Assert.Contains("2.", prompt);
        Assert.Contains("Missing tests", prompt);
        Assert.Contains("Log removed", prompt);
    }

    [Fact]
    public void SummarizeReport_StartsWithUserToken()
    {
        var prompt = PromptTemplates.SummarizeReport(["finding"]);
        Assert.StartsWith("<|user|>", prompt);
    }

    [Fact]
    public void SummarizeReport_EmptyFindings_StillProducesValidPrompt()
    {
        var prompt = PromptTemplates.SummarizeReport([]);
        Assert.StartsWith("<|user|>", prompt);
        Assert.EndsWith("<|assistant|>\n", prompt);
    }
}
