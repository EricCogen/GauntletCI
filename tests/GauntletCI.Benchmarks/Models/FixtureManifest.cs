// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json.Serialization;

namespace GauntletCI.Benchmarks.Models;

public class FixtureManifest
{
    [JsonPropertyName("mapped_gci_rules")]
    public List<string> MappedGciRules { get; set; } = [];

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fixtures")]
    public List<FixtureEntry> Fixtures { get; set; } = [];
}

public class FixtureEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("diff_file")]
    public string DiffFile { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("expected_outcome")]
    public string ExpectedOutcome { get; set; } = string.Empty;

    [JsonPropertyName("expected_gci_rules")]
    public List<string> ExpectedGciRules { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; set; } = string.Empty;

    [JsonPropertyName("source_url")]
    public string SourceUrl { get; set; } = string.Empty;
}
