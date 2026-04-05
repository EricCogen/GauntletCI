using System.Text.Json;

namespace GauntletCI.Benchmarks;

public static class FixtureLoader
{
    private static readonly string FixturesRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads the manifest and all diff content for a named fixture set (e.g. "pcg0001").
    /// Returns (manifest, dictionary of fixture id → diff text).
    /// </summary>
    public static (BenchmarkManifest Manifest, IReadOnlyDictionary<string, string> Diffs)
        Load(string fixtureSetName, string category = "curated")
    {
        string manifestPath = Path.Combine(FixturesRoot, category, fixtureSetName, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Benchmark manifest not found: {manifestPath}");

        string json = File.ReadAllText(manifestPath);
        BenchmarkManifest manifest = JsonSerializer.Deserialize<BenchmarkManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize manifest: {manifestPath}");

        string fixtureDir = Path.GetDirectoryName(manifestPath)!;
        Dictionary<string, string> diffs = [];
        foreach (BenchmarkFixture fixture in manifest.Fixtures)
        {
            string diffPath = Path.Combine(fixtureDir, fixture.DiffFile);
            diffs[fixture.Id] = File.ReadAllText(diffPath);
        }

        return (manifest, diffs);
    }

    /// <summary>
    /// Strips the leading comment header lines (lines starting with #) from a curated
    /// diff file to produce a clean unified diff suitable for passing to PromptBuilder.
    /// </summary>
    public static string StripHeader(string diffContent)
    {
        IEnumerable<string> lines = diffContent
            .Split('\n')
            .SkipWhile(l => l.TrimStart().StartsWith('#') || string.IsNullOrWhiteSpace(l));
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Parses the # expected: header value from a curated diff file.
    /// Returns "fire", "do-not-fire", or null if no header is present.
    /// </summary>
    public static string? ReadExpectedOutcome(string diffContent)
    {
        foreach (string line in diffContent.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("# expected:", StringComparison.OrdinalIgnoreCase))
                return trimmed["# expected:".Length..].Trim();
            if (!trimmed.StartsWith('#') && !string.IsNullOrWhiteSpace(trimmed))
                break;
        }
        return null;
    }
}
