// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;
using Xunit.Abstractions;

namespace GauntletCI.Benchmarks;

/// <summary>
/// Live LLM evaluation tests against the curated fixture corpus.
/// These tests are SKIPPED in CI unless ANTHROPIC_API_KEY or OPENAI_API_KEY is set.
/// Run locally to validate model accuracy against known-good fixtures.
///
/// Synthetic fixtures (origin: synthetic) are included by default and are always
/// marked with an explicit warning banner in test output. Set
/// GAUNTLETCI_INCLUDE_SYNTHETIC=0 to exclude synthetic fixtures from all-corpora runs.
///
/// These tests call PromptBuilder → ILlmClient → FindingParser directly,
/// bypassing gate infrastructure so an isolated diff can be evaluated without
/// a real Git working directory.
/// </summary>
public sealed class LiveEvaluationBenchmarkTests(ITestOutputHelper output)
{
    private static readonly string? AnthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    private static readonly string? OpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private static readonly bool LiveTestsEnabled = !string.IsNullOrWhiteSpace(AnthropicKey) || !string.IsNullOrWhiteSpace(OpenAiKey);
    private static readonly bool IncludeSynthetic =
        !string.Equals(Environment.GetEnvironmentVariable("GAUNTLETCI_INCLUDE_SYNTHETIC"), "0", StringComparison.Ordinal);

    private static (string Model, string ApiKey) ResolveModelAndKey()
    {
        if (!string.IsNullOrWhiteSpace(AnthropicKey))
            return ("claude-sonnet-4-6", AnthropicKey!);
        if (!string.IsNullOrWhiteSpace(OpenAiKey))
            return ("gpt-4o", OpenAiKey!);
        throw new InvalidOperationException("No API key available.");
    }

    private void WarnIfSynthetic(string fixtureId, bool isSynthetic)
    {
        if (!isSynthetic) return;
        output.WriteLine("┌─────────────────────────────────────────────────────────────────┐");
        output.WriteLine($"│  ⚠  SYNTHETIC FIXTURE: {fixtureId,-43}│");
        output.WriteLine("│     This diff was hand-authored, not sourced from a real commit. │");
        output.WriteLine("│     A passing result does NOT indicate real-world accuracy.      │");
        output.WriteLine("└─────────────────────────────────────────────────────────────────┘");
    }

    /// <summary>
    /// Evaluates every "fire" fixture in the gci0001 corpus and asserts that at
    /// least one of the expected GCI rules appears in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Gci0001FireFixtures))]
    public async Task LiveEval_FireFixture_ProducesAtLeastOneExpectedRule(
        string fixtureId,
        string diff,
        IReadOnlyList<string> expectedGciRules,
        bool isSynthetic)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");
        Skip.If(fixtureId == "<none>", "Skipped: no fire fixtures are available in gci0001.");
        WarnIfSynthetic(fixtureId, isSynthetic);

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        bool anyExpectedRuleFired = expectedGciRules.Any(r => firedRuleIds.Contains(r));

        Assert.True(anyExpectedRuleFired,
            $"Fixture {fixtureId}: expected one of [{string.Join(", ", expectedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", firedRuleIds)}]");
    }

    /// <summary>
    /// Evaluates every "do-not-fire" fixture in the gci0001 corpus and asserts
    /// that none of the mapped GCI rules appear in the findings.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Gci0001DoNotFireFixtures))]
    public async Task LiveEval_DoNotFireFixture_ProducesNoMappedRuleFindings(
        string fixtureId,
        string diff,
        IReadOnlyList<string> mappedGciRules,
        bool isSynthetic)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");
        Skip.If(fixtureId == "<none>", "Skipped: no do-not-fire fixtures are available in gci0001.");
        WarnIfSynthetic(fixtureId, isSynthetic);

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
        bool yieldedAny = false;
        foreach (BenchmarkFixture fixture in manifest.Fixtures.Where(f => f.ShouldFire))
        {
            yieldedAny = true;
            yield return new object[]
            {
                fixture.Id,
                FixtureLoader.StripHeader(diffs[fixture.Id]),
                fixture.ExpectedGciRules,
                fixture.IsSynthetic,
            };
        }

        if (!yieldedAny)
            yield return new object[] { "<none>", string.Empty, Array.Empty<string>(), false };
    }

    public static IEnumerable<object[]> Gci0001DoNotFireFixtures()
    {
        var (manifest, diffs) = FixtureLoader.Load("gci0001");
        bool yieldedAny = false;
        foreach (BenchmarkFixture fixture in manifest.Fixtures.Where(f => !f.ShouldFire))
        {
            yieldedAny = true;
            yield return new object[]
            {
                fixture.Id,
                FixtureLoader.StripHeader(diffs[fixture.Id]),
                manifest.MappedGciRules,
                fixture.IsSynthetic,
            };
        }

        if (!yieldedAny)
            yield return new object[] { "<none>", string.Empty, Array.Empty<string>(), false };
    }

    public static IEnumerable<object[]> AllFireFixtures()
    {
        bool yieldedAny = false;
        foreach (string dir in Directory.EnumerateDirectories(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "curated")))
        {
            string setName = Path.GetFileName(dir);
            var (manifest, diffs) = FixtureLoader.Load(setName);
            foreach (var f in manifest.Fixtures.Where(x => x.ShouldFire && (IncludeSynthetic || !x.IsSynthetic)))
            {
                yieldedAny = true;
                yield return new object[] { setName, f.Id, FixtureLoader.StripHeader(diffs[f.Id]), f.ExpectedGciRules, f.IsSynthetic };
            }
        }

        if (!yieldedAny)
            yield return new object[] { "<none>", "<none>", string.Empty, Array.Empty<string>(), false };
    }

    public static IEnumerable<object[]> AllDoNotFireFixtures()
    {
        bool yieldedAny = false;
        foreach (string dir in Directory.EnumerateDirectories(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "curated")))
        {
            string setName = Path.GetFileName(dir);
            var (manifest, diffs) = FixtureLoader.Load(setName);
            foreach (var f in manifest.Fixtures.Where(x => !x.ShouldFire && (IncludeSynthetic || !x.IsSynthetic)))
            {
                yieldedAny = true;
                yield return new object[] { setName, f.Id, FixtureLoader.StripHeader(diffs[f.Id]), manifest.MappedGciRules, f.IsSynthetic };
            }
        }

        if (!yieldedAny)
            yield return new object[] { "<none>", "<none>", string.Empty, Array.Empty<string>(), false };
    }

    /// <summary>
    /// Evaluates every "fire" fixture across all corpora and asserts at least one
    /// expected GCI rule fires. Synthetic fixtures are included by default and
    /// can be excluded with GAUNTLETCI_INCLUDE_SYNTHETIC=0.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllFireFixtures))]
    public async Task LiveEval_AllCorpora_FireFixture_ProducesAtLeastOneExpectedRule(
        string fixtureSetName,
        string fixtureId,
        string diff,
        IReadOnlyList<string> expectedGciRules,
        bool isSynthetic)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");
        Skip.If(fixtureId == "<none>", "Skipped: no fixtures matched the configured synthetic filter.");
        WarnIfSynthetic(fixtureId, isSynthetic);

        IReadOnlyList<Finding> findings = await EvaluateDiffAsync(diff);

        IEnumerable<string> firedRuleIds = findings.Select(f => f.RuleId);
        bool anyExpectedRuleFired = expectedGciRules.Any(r => firedRuleIds.Contains(r));

        Assert.True(anyExpectedRuleFired,
            $"[{fixtureSetName}] Fixture {fixtureId}: expected one of [{string.Join(", ", expectedGciRules)}] to fire, " +
            $"but got: [{string.Join(", ", firedRuleIds)}]");
    }

    /// <summary>
    /// Evaluates every "do-not-fire" fixture across all corpora and asserts none of
    /// the mapped GCI rules fire. Synthetic fixtures are included by default and
    /// can be excluded with GAUNTLETCI_INCLUDE_SYNTHETIC=0.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllDoNotFireFixtures))]
    public async Task LiveEval_AllCorpora_DoNotFireFixture_ProducesNoMappedRuleFindings(
        string fixtureSetName,
        string fixtureId,
        string diff,
        IReadOnlyList<string> mappedGciRules,
        bool isSynthetic)
    {
        Skip.IfNot(LiveTestsEnabled, "Skipped: no ANTHROPIC_API_KEY or OPENAI_API_KEY set.");
        Skip.If(fixtureId == "<none>", "Skipped: no fixtures matched the configured synthetic filter.");
        WarnIfSynthetic(fixtureId, isSynthetic);

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
