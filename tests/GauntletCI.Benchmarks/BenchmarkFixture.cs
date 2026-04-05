using System.Text.Json.Serialization;

namespace GauntletCI.Benchmarks;

public sealed record BenchmarkFixture(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("diff_file")] string DiffFile,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("expected_outcome")] string ExpectedOutcome,
    [property: JsonPropertyName("expected_gci_rules")] IReadOnlyList<string> ExpectedGciRules,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("origin")] string Origin = "synthetic",
    [property: JsonPropertyName("source_url")] string? SourceUrl = null,
    [property: JsonPropertyName("runtime_state_condition")] string? RuntimeStateCondition = null)
{
    public bool ShouldFire => string.Equals(ExpectedOutcome, "fire", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the diff was hand-authored for harness validation rather than sourced from a real commit.
    /// Synthetic fixtures cannot be used to measure model accuracy.
    /// </summary>
    public bool IsSynthetic => string.Equals(Origin, "synthetic", StringComparison.OrdinalIgnoreCase);
}

public sealed record BenchmarkManifest(
    [property: JsonPropertyName("mapped_gci_rules")] IReadOnlyList<string> MappedGciRules,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fixtures")] IReadOnlyList<BenchmarkFixture> Fixtures);
