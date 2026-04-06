using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Tests;

public sealed class FindingParserAndContextAssemblerTests
{
    [Fact]
    public void FindingParser_Parses_ValidFindingArray()
    {
        string rawJson =
            """
            [
              {
                "rule_id": "GCI003",
                "rule_name": "Behavioral Change Detection",
                "severity": "high",
                "finding": "OrderProcessor now swallows exception and changes caller behavior.",
                "evidence": "OrderProcessor.cs:47",
                "why_it_matters": "Contract changed.",
                "suggested_action": "Restore throw behavior.",
                "confidence": "High"
              }
            ]
            """;

        FindingParser parser = new();
        IReadOnlyList<Finding> findings = parser.Parse(rawJson);

        Assert.Single(findings);
        Assert.Equal("GCI003", findings[0].RuleId);
        Assert.Equal("high", findings[0].Severity);
    }

    [Fact]
    public void ContextAssembler_TrimmedDiff_AddsTrimNotice()
    {
        ContextAssembler assembler = new();
        GateResult branch = GateResult.Pass("Branch Currency", "ok");
        GateResult test = GateResult.Pass("Test Passage", "ok");
        GauntletConfig config = new();
        string diff = string.Join('\n', Enumerable.Range(1, 100).Select(static i => $"+ line {i}"));

        AssembledContext assembled = assembler.Assemble(branch, test, diff, config, ["feat: setup"], 20);

        Assert.True(assembled.DiffTrimmed);
        Assert.Contains("diff trimmed", assembled.Context, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindingParser_DropsVagueFindingWithoutConcreteEvidence()
    {
        string rawJson =
            """
            [
              {
                "rule_id": "GCI007",
                "rule_name": "Error Handling Integrity",
                "severity": "medium",
                "finding": "Error handling may need review",
                "evidence": "something unclear",
                "why_it_matters": "might be bad",
                "suggested_action": "review it",
                "confidence": "Low"
              }
            ]
            """;

        FindingParser parser = new();
        IReadOnlyList<Finding> findings = parser.Parse(rawJson);

        Assert.Empty(findings);
    }
}
