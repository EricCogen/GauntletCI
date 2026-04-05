using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Benchmarks;

/// <summary>
/// Live LLM evaluation tests against the curated fixture corpus.
/// These tests are SKIPPED in CI unless ANTHROPIC_API_KEY or OPENAI_API_KEY is set.
/// Run locally to validate model accuracy against known-good fixtures.
///
/// These tests call PromptBuilder → ILlmClient → FindingParser directly,
/// bypassing gate infrastructure so an isolated diff can be evaluated without
/// a real Git working directory.
/// </summary>
public sealed class LiveEvaluationBenchmarkTests
{
    private static readonly string? AnthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    private static readonly string? OpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private static readonly bool LiveTestsEnabled = !string.IsNullOrWhiteSpace(AnthropicKey) || !string.IsNullOrWhiteSpace(OpenAiKey);

    private static (string Model, string ApiKey) ResolveModelAndKey()
    {
        if (!string.IsNullOrWhiteSpace(AnthropicKey))
            return ("claude-sonnet-4-5", AnthropicKey!);
        if (!string.IsNullOrWhiteSpace(OpenAiKey))
            return ("gpt-4o", OpenAiKey!);
        throw new InvalidOperationException("No API key available.");
    }

    /// <summary>
    /// Evaluates every "fire" fixture in the pcg0001 corpus and asserts that at
    /// least one of the expected GCI rules appears in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Gci0001FireFixtures))]
    public async Task LiveEval_FireFixture_ProducesAtLeastOneExpectedRule(
        string fixtureId,
        string diff,
        IReadOnlyList<string> expectedGciRules)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        bool anyExpectedRuleFired = expectedGciRules.Any(r => firedRuleIds.Contains(r));

        Assert.True(anyExpectedRuleFired,
            $"Fixture {fixtureId}: expected one of [{string.Join(", ", expectedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", firedRuleIds)}]");
    }

    /// <summary>
    /// Evaluates every "do-not-fire" fixture in the pcg0001 corpus and asserts
    /// that none of the mapped GCI rules appear in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Gci0001DoNotFireFixtures))]
    public async Task LiveEval_DoNotFireFixture_ProducesNoMappedRuleFindings(
        string fixtureId,
        string diff,
        IReadOnlyList<string> mappedGciRules)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        IEnumerable<string> unexpectedFired = mappedGciRules.Where(r => firedRuleIds.Contains(r));

        Assert.True(!unexpectedFired.Any(),
            $"Fixture {fixtureId}: expected none of [{string.Join(", ", mappedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", unexpectedFired)}]");
    }

    public static IEnumerable<object[]> Gci0001FireFixtures()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");
        return manifest.Fixtures
            .Where(f => f.ShouldFire)
            .Select(f => new object[]
            {
                f.Id,
                FixtureLoader.StripHeader(diffs[f.Id]),
                f.ExpectedGciRules,
            });
    }

    public static IEnumerable<object[]> Gci0001DoNotFireFixtures()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");
        return manifest.Fixtures
            .Where(f => !f.ShouldFire)
            .Select(f => new object[]
            {
                f.Id,
                FixtureLoader.StripHeader(diffs[f.Id]),
                manifest.MappedGciRules,
            });
    }

    public static IEnumerable<object[]> AllFireFixtures()
    {
        foreach (string dir in Directory.EnumerateDirectories(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "curated")))
        {
            string setName = Path.GetFileName(dir);
            var (manifest, diffs) = FixtureLoader.Load(setName);
            foreach (var f in manifest.Fixtures.Where(x => x.ShouldFire))
                yield return new object[] { setName, f.Id, FixtureLoader.StripHeader(diffs[f.Id]), f.ExpectedGciRules };
        }
    }

    public static IEnumerable<object[]> AllDoNotFireFixtures()
    {
        foreach (string dir in Directory.EnumerateDirectories(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "curated")))
        {
            string setName = Path.GetFileName(dir);
            var (manifest, diffs) = FixtureLoader.Load(setName);
            foreach (var f in manifest.Fixtures.Where(x => !x.ShouldFire))
                yield return new object[] { setName, f.Id, FixtureLoader.StripHeader(diffs[f.Id]), manifest.MappedGciRules };
        }
    }

    /// <summary>
    /// Evaluates every "fire" fixture across all corpora (gci0001–gci0018) and asserts
    /// that at least one of the expected GCI rules appears in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllFireFixtures))]
    public async Task LiveEval_AllCorpora_FireFixture_ProducesAtLeastOneExpectedRule(
        string fixtureSetName,
        string fixtureId,
        string diff,
        IReadOnlyList<string> expectedGciRules)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        bool anyExpectedRuleFired = expectedGciRules.Any(r => firedRuleIds.Contains(r));

        Assert.True(anyExpectedRuleFired,
            $"[{fixtureSetName}] Fixture {fixtureId}: expected one of [{string.Join(", ", expectedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", firedRuleIds)}]");
    }

    /// <summary>
    /// Evaluates every "do-not-fire" fixture across all corpora (gci0001–gci0018) and asserts
    /// that none of the mapped GCI rules appear in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllDoNotFireFixtures))]
    public async Task LiveEval_AllCorpora_DoNotFireFixture_ProducesNoMappedRuleFindings(
        string fixtureSetName,
        string fixtureId,
        string diff,
        IReadOnlyList<string> mappedGciRules)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        IEnumerable<string> unexpectedFired = mappedGciRules.Where(r => firedRuleIds.Contains(r));

        Assert.True(!unexpectedFired.Any(),
            $"[{fixtureSetName}] Fixture {fixtureId}: expected none of [{string.Join(", ", mappedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", unexpectedFired)}]");
    }

    private static async Task<IReadOnlyList<Finding>> EvaluateDiffAsync(string diff)
    {
        (string model, string apiKey) = ResolveModelAndKey();

        RulesTextProvider rulesTextProvider = new();
        PromptBuilder promptBuilder = new();
        FindingParser findingParser = new();
        HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
        ILlmClient llmClient = new HttpLlmClient(httpClient);

        string rulesText = rulesTextProvider.LoadRulesText();
        string systemPrompt = promptBuilder.BuildSystemPrompt(rulesText, singleRule: null);
        string userPrompt = promptBuilder.BuildUserPrompt(diff);

        LlmResponse response = await llmClient.EvaluateAsync(model, systemPrompt, userPrompt, apiKey, CancellationToken.None);
        if (!response.Success)
            throw new InvalidOperationException($"LLM call failed: {response.ErrorMessage}");

        return findingParser.Parse(response.RawResponse);
    }
}
