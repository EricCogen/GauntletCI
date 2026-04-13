// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Benchmarks.Models;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;

namespace GauntletCI.Benchmarks;

public class CuratedFixtureTests
{
    private static readonly string FixturesRoot = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "curated");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> AllFixtures()
    {
        if (!Directory.Exists(FixturesRoot))
            yield break;

        foreach (var dir in Directory.GetDirectories(FixturesRoot).OrderBy(d => d))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            FixtureManifest manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonSerializer.Deserialize<FixtureManifest>(json, JsonOpts)!;
            }
            catch { continue; }

            foreach (var entry in manifest.Fixtures)
            {
                var diffPath = Path.Combine(dir, entry.DiffFile);
                if (!File.Exists(diffPath)) continue;
                yield return [entry.Id, diffPath, entry];
            }
        }
    }

    // Rules that shifted IDs between the MISC fixture version and v2 codebase.
    // Key = short MISC ID, Value = canonical v2 rule ID.
    private static readonly Dictionary<string, string> RuleIdRemap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GCI017"] = "GCI_SYN_AGG", // Production Readiness moved to synthetic aggregate ID
            ["GCI018"] = "GCI0020",     // Accountability Standard moved from slot 18 → 20 (GCI0018 is now Production Readiness)
        };

    private static string NormalizeRuleId(string id)
    {
        if (RuleIdRemap.TryGetValue(id, out var remapped)) return remapped;
        if (id.StartsWith("GCI", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(id[3..], out int n))
            return $"GCI{n:D4}";
        return id;
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    [Trait("Category", "Benchmark")]
    public async Task FixtureAssertions(string fixtureId, string diffPath, FixtureEntry entry)
    {
        var rawDiff = await File.ReadAllTextAsync(diffPath);
        var diff = DiffParser.Parse(rawDiff);
        var orchestrator = RuleOrchestrator.CreateDefault();
        var result = await orchestrator.RunAsync(diff);

        var firedRuleIds = result.Findings.Select(f => f.RuleId).Distinct().ToHashSet();
        var expectedIds = entry.ExpectedGciRules.Select(NormalizeRuleId).ToList();

        if (entry.ExpectedOutcome == "fire")
        {
            if (expectedIds.Count > 0)
                Assert.True(
                    expectedIds.Any(id => firedRuleIds.Contains(id)),
                    $"[{fixtureId}] Expected one of [{string.Join(", ", expectedIds)}] to fire but got: [{string.Join(", ", firedRuleIds)}]");
            else
                Assert.True(
                    result.HasFindings,
                    $"[{fixtureId}] Expected at least one finding but engine returned none.");
        }
        else if (entry.ExpectedOutcome == "do-not-fire")
        {
            if (expectedIds.Count > 0)
                Assert.False(
                    expectedIds.Any(id => firedRuleIds.Contains(id)),
                    $"[{fixtureId}] Expected none of [{string.Join(", ", expectedIds)}] to fire but got: [{string.Join(", ", firedRuleIds)}]");
            else
                Assert.False(
                    result.HasFindings,
                    $"[{fixtureId}] Expected no findings but engine returned: [{string.Join(", ", firedRuleIds)}]");
        }
        // edge-case entries are not asserted — they're for observation only
    }
}
