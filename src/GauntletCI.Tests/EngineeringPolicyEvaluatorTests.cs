// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Tests;

public class EngineeringPolicyEvaluatorTests
{
    private static DiffContext EmptyDiff() => DiffParser.Parse(string.Empty);

    [Fact]
    public async Task EvaluateAsync_returns_empty_when_llm_not_available()
    {
        var llm = new StubLlmEngine(isAvailable: false, response: "[]");

        var result = await EngineeringPolicyEvaluator.EvaluateAsync(
            EmptyDiff(), "nonexistent.md", llm);

        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateAsync_returns_empty_when_policy_file_missing()
    {
        var llm = new StubLlmEngine(isAvailable: true, response: "[]");

        var result = await EngineeringPolicyEvaluator.EvaluateAsync(
            EmptyDiff(), Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".md"), llm);

        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateAsync_returns_advisory_findings_from_llm_response()
    {
        var policyFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(policyFile, "## EP001 · Correctness and Intent · High");

            var llm = new StubLlmEngine(isAvailable: true, response: """
                [
                  {
                    "ruleId": "EP001",
                    "ruleName": "Correctness and Intent",
                    "summary": "TODO comment left in added code",
                    "evidence": "Foo.cs:10 — // TODO: implement",
                    "whyItMatters": "Implies incomplete implementation.",
                    "suggestedAction": "Remove TODO or implement before merging."
                  }
                ]
                """);

            var result = await EngineeringPolicyEvaluator.EvaluateAsync(
                EmptyDiff(), policyFile, llm);

            Assert.Single(result);
            Assert.Equal("EP001", result[0].RuleId);
            Assert.Equal(RuleSeverity.Advisory, result[0].Severity);
        }
        finally { File.Delete(policyFile); }
    }

    [Fact]
    public async Task EvaluateAsync_returns_empty_when_llm_returns_empty_array()
    {
        var policyFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(policyFile, "## EP001");
            var llm = new StubLlmEngine(isAvailable: true, response: "[]");
            var result = await EngineeringPolicyEvaluator.EvaluateAsync(EmptyDiff(), policyFile, llm);
            Assert.Empty(result);
        }
        finally { File.Delete(policyFile); }
    }

    [Fact]
    public async Task EvaluateAsync_returns_empty_when_llm_returns_invalid_json()
    {
        var policyFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(policyFile, "## EP001");
            var llm = new StubLlmEngine(isAvailable: true, response: "not json at all");
            var result = await EngineeringPolicyEvaluator.EvaluateAsync(EmptyDiff(), policyFile, llm);
            Assert.Empty(result);
        }
        finally { File.Delete(policyFile); }
    }

    [Fact]
    public async Task EvaluateAsync_strips_markdown_fences_from_llm_response()
    {
        var policyFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(policyFile, "## EP001");
            var llm = new StubLlmEngine(isAvailable: true, response: """
                ```json
                [{"ruleId":"EP001","ruleName":"Correctness","summary":"s","evidence":"e","whyItMatters":"w","suggestedAction":"a"}]
                ```
                """);
            var result = await EngineeringPolicyEvaluator.EvaluateAsync(EmptyDiff(), policyFile, llm);
            Assert.Single(result);
            Assert.Equal("EP001", result[0].RuleId);
        }
        finally { File.Delete(policyFile); }
    }

    [Fact]
    public async Task EvaluateAsync_parses_json_when_response_has_preamble_text()
    {
        var policyFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(policyFile, "## EP001");
            var llm = new StubLlmEngine(isAvailable: true, response: """
                Here are the findings I found:
                [{"ruleId":"EP001","ruleName":"Correctness","summary":"s","evidence":"e","whyItMatters":"w","suggestedAction":"a"}]
                """);
            var result = await EngineeringPolicyEvaluator.EvaluateAsync(EmptyDiff(), policyFile, llm);
            Assert.Single(result);
            Assert.Equal("EP001", result[0].RuleId);
        }
        finally { File.Delete(policyFile); }
    }

    private sealed class StubLlmEngine(bool isAvailable, string response) : ILlmEngine
    {
        public bool IsAvailable => isAvailable;
        public Task<string> CompleteAsync(string prompt, CancellationToken ct) => Task.FromResult(response);
        public Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct) => Task.FromResult(string.Empty);
        public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct) => Task.FromResult(string.Empty);
        public void Dispose() { }
    }
}
