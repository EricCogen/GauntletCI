using System.Text.Json.Serialization;

namespace GauntletCI.Benchmarks;

public sealed record BenchmarkFixture(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("diff_file")] string DiffFile,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("expected_outcome")] string ExpectedOutcome,
    [property: JsonPropertyName("expected_gci_rules")] IReadOnlyList<string> ExpectedGciRules,
    [property: JsonPropertyName("notes")] string Notes)
{
    public bool ShouldFire => string.Equals(ExpectedOutcome, "fire", StringComparison.OrdinalIgnoreCase);
}

public sealed record BenchmarkManifest(
    [property: JsonPropertyName("source_pcg_rule")] string SourcePcgRule,
    [property: JsonPropertyName("mapped_gci_rules")] IReadOnlyList<string> MappedGciRules,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fixtures")] IReadOnlyList<BenchmarkFixture> Fixtures);
