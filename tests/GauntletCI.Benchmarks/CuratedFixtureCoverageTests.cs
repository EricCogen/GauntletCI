// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Benchmarks.Models;

namespace GauntletCI.Benchmarks;

public class CuratedFixtureCoverageTests
{
    private static readonly string FixturesRoot = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "curated");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Manifests_MustNotBeEmptyShells()
    {
        Assert.True(Directory.Exists(FixturesRoot), $"Fixtures root missing: {FixturesRoot}");

        var emptyManifests = new List<string>();
        foreach (var dir in Directory.GetDirectories(FixturesRoot).OrderBy(d => d))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<FixtureManifest>(json, JsonOpts);
            if (manifest?.Fixtures is null || manifest.Fixtures.Count == 0)
                emptyManifests.Add(Path.GetFileName(dir));
        }

        Assert.True(
            emptyManifests.Count == 0,
            "Curated manifests must include at least one fixture entry. Empty shells: "
            + string.Join(", ", emptyManifests));
    }
}
