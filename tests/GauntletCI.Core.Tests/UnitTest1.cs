using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Tests;

public class UnitTest1
{
    [Fact]
    public void FindingParser_Parses_ValidFindingArray()
    {
        string rawJson =
            """
            [
              {
                "rule_id": "FL003",
                "rule_name": "Behavioral Change Detection",
                "severity": "high",
                "finding": "Behavior changed.",
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
        Assert.Equal("FL003", findings[0].RuleId);
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

        (string context, bool trimmed) = assembler.Assemble(branch, test, diff, config, ["feat: setup"], 20);

        Assert.True(trimmed);
        Assert.Contains("diff trimmed", context, StringComparison.OrdinalIgnoreCase);
    }
}