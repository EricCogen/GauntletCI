using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Benchmarks;

/// <summary>
/// Structural tests that verify fixture loading, header parsing, prompt assembly,
/// and the PCG→GCI rule mapping — no live LLM calls required.
/// </summary>
public sealed class CuratedFixtureStructureTests
{
    [Fact]
    public void Load_Gci0001_ReturnsAllTwelveFixtures()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        Assert.Equal(12, manifest.Fixtures.Count);
        Assert.Equal(12, diffs.Count);
    }

    [Fact]
    public void Load_Gci0001_ManifestMappedToGciRules()
    {
        var (manifest, _) = FixtureLoader.Load("gci0001");

        Assert.Contains("GCI006", manifest.MappedGciRules);
        Assert.Contains("GCI005", manifest.MappedGciRules);
    }

    [Theory]
    [InlineData("gci0001-01", "fire")]
    [InlineData("gci0001-02", "fire")]
    [InlineData("gci0001-03", "fire")]
    [InlineData("gci0001-04", "do-not-fire")]
    [InlineData("gci0001-05", "do-not-fire")]
    [InlineData("gci0001-07", "fire")]
    [InlineData("gci0001-11", "do-not-fire")]
    [InlineData("gci0001-12", "do-not-fire")]
    public void Manifest_ExpectedOutcome_MatchesDiffHeader(string fixtureId, string expectedOutcome)
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        BenchmarkFixture fixture = manifest.Fixtures.Single(f => f.Id == fixtureId);
        string headerOutcome = FixtureLoader.ReadExpectedOutcome(diffs[fixtureId])!;

        // Manifest and diff header must agree
        Assert.Equal(expectedOutcome, fixture.ExpectedOutcome);
        Assert.Equal(expectedOutcome, headerOutcome);
    }

    [Theory]
    [InlineData("gci0001-01")]
    [InlineData("gci0001-04")]
    [InlineData("gci0001-12")]
    public void StripHeader_RemovesCommentLines_LeavingValidUnifiedDiff(string fixtureId)
    {
        var (_, diffs) = FixtureLoader.Load("gci0001");

        string stripped = FixtureLoader.StripHeader(diffs[fixtureId]);
        string firstLine = stripped.TrimStart().Split('\n').First(l => !string.IsNullOrWhiteSpace(l));

        Assert.StartsWith("---", firstLine);
    }

    [Theory]
    [InlineData("gci0001-01")]
    [InlineData("gci0001-04")]
    [InlineData("gci0001-08")]
    public void PromptBuilder_ProducesNonEmptySystemPrompt_FromCuratedDiff(string fixtureId)
    {
        var (_, diffs) = FixtureLoader.Load("gci0001");

        string diff = FixtureLoader.StripHeader(diffs[fixtureId]);
        PromptBuilder pb = new();
        string systemPrompt = pb.BuildSystemPrompt("(rules placeholder)", singleRule: null);
        string userPrompt = pb.BuildUserPrompt(diff);

        Assert.NotEmpty(systemPrompt);
        Assert.Contains("GCI001", systemPrompt);
        Assert.NotEmpty(userPrompt);
        // User prompt should contain at least one diff marker
        Assert.True(userPrompt.Contains("---") || userPrompt.Contains("@@"),
            "User prompt should contain unified diff content");
    }

    [Theory]
    [InlineData("PCG0001", "GCI005")]
    [InlineData("PCG0002", "GCI003")]
    [InlineData("PCG0003", "GCI006")]
    [InlineData("PCG0004", "GCI016")]
    [InlineData("PCG0005", "GCI004")]
    [InlineData("PCG0006", "GCI006")]
    [InlineData("PCG0007", "GCI007")]
    [InlineData("PCG0008", "GCI008")]
    [InlineData("PCG0009", "GCI009")]
    [InlineData("PCG0010", "GCI010")]
    [InlineData("PCG0011", "GCI011")]
    [InlineData("PCG0012", "GCI012")]
    [InlineData("PCG0013", "GCI013")]
    [InlineData("PCG0014", "GCI014")]
    [InlineData("PCG0015", "GCI015")]
    [InlineData("PCG0016", "GCI016")]
    [InlineData("PCG0017", "GCI001")]
    [InlineData("PCG0018", "GCI017")]
    [InlineData("PCG0019", "GCI005")]
    [InlineData("PCG0020", "GCI018")]
    public void PcgToGciMap_AllTwentyRulesMapped(string pcgId, string expectedGciId)
    {
        string actual = PcgToGciRuleMap.ToGci(pcgId)!;
        Assert.Equal(expectedGciId, actual);
    }

    [Fact]
    public void PcgToGciMap_Normalize_PassthroughForGciIds()
    {
        Assert.Equal("GCI007", PcgToGciRuleMap.Normalize("GCI007"));
        Assert.Equal("GCI001", PcgToGciRuleMap.Normalize("GCI001"));
    }

    [Fact]
    public void PcgToGciMap_Normalize_ConvertsPcgIds()
    {
        Assert.Equal("GCI007", PcgToGciRuleMap.Normalize("PCG0007"));
        Assert.Equal("GCI016", PcgToGciRuleMap.Normalize("PCG0004"));
    }

    [Fact]
    public void PcgToGciMap_ToGci_ReturnsNullForUnknownId()
    {
        Assert.Null(PcgToGciRuleMap.ToGci("PCG9999"));
    }

    [Fact]
    public void AllCuratedDiffs_HaveValidExpectedHeader()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        foreach (BenchmarkFixture fixture in manifest.Fixtures)
        {
            string? headerOutcome = FixtureLoader.ReadExpectedOutcome(diffs[fixture.Id]);
            Assert.True(
                headerOutcome == "fire" || headerOutcome == "do-not-fire",
                $"Fixture {fixture.Id} has invalid or missing # expected header: '{headerOutcome}'");
        }
    }

    [Fact]
    public void FindingParser_ParsesValidJsonArray()
    {
        FindingParser parser = new();
        const string json = """
            [
                {
                    "rule_id": "GCI006",
                    "rule_name": "Edge Case Handling",
                    "severity": "high",
                    "finding": "Null guard added at method entry with no test coverage.",
                    "evidence": "UserService.cs:14 ProcessUser",
                    "why_it_matters": "Callers passing null will throw at runtime with no test asserting the contract.",
                    "suggested_action": "Add a unit test asserting ArgumentNullException for null input.",
                    "confidence": "High"
                }
            ]
            """;

        IReadOnlyList<Finding> findings = parser.Parse(json);

        Assert.Single(findings);
        Assert.Equal("GCI006", findings[0].RuleId);
    }
}
