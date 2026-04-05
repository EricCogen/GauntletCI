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
    public void Load_Gci0001_ReturnsFixtureManifestAndDiffs()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        Assert.Equal(manifest.Fixtures.Count, diffs.Count);
        Assert.All(manifest.Fixtures, f => Assert.True(diffs.ContainsKey(f.Id), $"Missing diff content for fixture {f.Id}."));
    }

    [Fact]
    public void Load_Gci0001_ManifestMappedToGciRules()
    {
        var (manifest, _) = FixtureLoader.Load("gci0001");

        Assert.Contains("GCI001", manifest.MappedGciRules);
    }

    [Fact]
    public void Manifest_ExpectedOutcome_MatchesDiffHeader_ForAllGci0001Fixtures()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        foreach (BenchmarkFixture fixture in manifest.Fixtures)
        {
            string headerOutcome = FixtureLoader.ReadExpectedOutcome(diffs[fixture.Id])!;
            Assert.Equal(fixture.ExpectedOutcome, headerOutcome);
        }
    }

    [Fact]
    public void StripHeader_RemovesCommentLines_LeavingValidUnifiedDiff()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");

        foreach (BenchmarkFixture fixture in manifest.Fixtures)
        {
            string stripped = FixtureLoader.StripHeader(diffs[fixture.Id]);
            string firstLine = stripped.TrimStart().Split('\n').First(l => !string.IsNullOrWhiteSpace(l));

            Assert.StartsWith("---", firstLine);
        }
    }

    [Fact]
    public void PromptBuilder_ProducesNonEmptySystemPrompt_FromCuratedDiffs()
    {
        PromptBuilder pb = new();
        int checkedDiffs = 0;
        foreach (string fixtureSetName in GetCuratedFixtureSetNames())
        {
            var (_, diffs) = FixtureLoader.Load(fixtureSetName);
            foreach (string rawDiff in diffs.Values)
            {
                string diff = FixtureLoader.StripHeader(rawDiff);
                string systemPrompt = pb.BuildSystemPrompt("(rules placeholder)", singleRule: null);
                string userPrompt = pb.BuildUserPrompt(diff);

                Assert.NotEmpty(systemPrompt);
                Assert.Contains("GCI001", systemPrompt);
                Assert.NotEmpty(userPrompt);
                Assert.True(userPrompt.Contains("---") || userPrompt.Contains("@@"),
                    "User prompt should contain unified diff content");

                checkedDiffs++;
            }
        }

        Assert.True(checkedDiffs > 0, "Expected at least one curated diff to validate prompt assembly.");
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
        foreach (string fixtureSetName in GetCuratedFixtureSetNames())
        {
            var (manifest, diffs) = FixtureLoader.Load(fixtureSetName);
            foreach (BenchmarkFixture fixture in manifest.Fixtures)
            {
                string? headerOutcome = FixtureLoader.ReadExpectedOutcome(diffs[fixture.Id]);
                Assert.True(
                    headerOutcome == "fire" || headerOutcome == "do-not-fire",
                    $"Fixture {fixture.Id} has invalid or missing # expected header: '{headerOutcome}'");
            }
        }
    }

    private static IEnumerable<string> GetCuratedFixtureSetNames()
    {
        string curatedRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "curated");
        foreach (string dir in Directory.EnumerateDirectories(curatedRoot))
        {
            yield return Path.GetFileName(dir);
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
