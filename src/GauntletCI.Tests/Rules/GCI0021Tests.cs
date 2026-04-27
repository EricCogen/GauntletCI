// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0021Tests
{
    private static readonly GCI0021_DataSchemaCompatibility Rule = new();

    [Fact]
    public async Task RemovedJsonPropertyName_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Dto.cs b/src/Dto.cs
            index abc..def 100644
            --- a/src/Dto.cs
            +++ b/src/Dto.cs
            @@ -1,3 +1,2 @@
             public class Dto {
            -    [JsonPropertyName("user_id")]
                 public int UserId { get; set; }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Serialization attribute removed") &&
            f.Summary.Contains("JsonPropertyName"));
    }

    [Fact]
    public async Task RemovedEnumMember_WithSerializationAttr_ShouldFlag()
    {
        // Enum member removed WITH a preceding [JsonProperty] attribute — should flag.
        var raw = """
            diff --git a/src/Status.cs b/src/Status.cs
            index abc..def 100644
            --- a/src/Status.cs
            +++ b/src/Status.cs
            @@ -1,6 +1,4 @@
             public enum Status {
                 Active,
            -    [JsonProperty("suspended")]
            -    Suspended,
                 Deleted,
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Enum member removed") &&
            f.Summary.Contains("Suspended"));
    }

    [Fact]
    public async Task RemovedEnumMember_WithoutSerializationAttr_ShouldNotFlag()
    {
        // Enum member removed WITHOUT a serialization attribute — not a schema compat concern.
        var raw = """
            diff --git a/src/Status.cs b/src/Status.cs
            index abc..def 100644
            --- a/src/Status.cs
            +++ b/src/Status.cs
            @@ -1,5 +1,4 @@
             public enum Status {
                 Active,
            -    Suspended,
                 Deleted,
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Enum member removed"));
    }

    [Fact]
    public async Task RemovedSerializationAttr_AddedToNonCsFile_ShouldNotFlag()
    {
        // Non-.cs file — rule should ignore it
        var raw = """
            diff --git a/config.json b/config.json
            index abc..def 100644
            --- a/config.json
            +++ b/config.json
            @@ -1,2 +1,1 @@
            -    "[JsonPropertyName]": "legacy"
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task RemovedColumnAttribute_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Entity.cs b/src/Entity.cs
            index abc..def 100644
            --- a/src/Entity.cs
            +++ b/src/Entity.cs
            @@ -1,3 +1,2 @@
             public class Entity {
            -    [Column("first_name")]
                 public string Name { get; set; }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Serialization attribute removed"));
    }

    [Fact]
    public async Task DictionaryIndexerWithKeyVariable_ShouldNotFlag()
    {
        // dictionary[key] contains "[key]" which previously matched [Key] case-insensitively.
        var raw = """
            diff --git a/src/Store.cs b/src/Store.cs
            index abc..def 100644
            --- a/src/Store.cs
            +++ b/src/Store.cs
            @@ -1,3 +1,2 @@
             public class Store {
            -    dictionary[key] = value + 1;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Serialization attribute removed"));
    }

    [Fact]
    public async Task FieldNameLookupIndexer_ShouldNotFlag()
    {
        // fieldNameLookup[key] = i was a false positive via [key] matching [Key].
        var raw = """
            diff --git a/src/Mapper.cs b/src/Mapper.cs
            index abc..def 100644
            --- a/src/Mapper.cs
            +++ b/src/Mapper.cs
            @@ -1,3 +1,2 @@
             public class Mapper {
            -    if (key != null) fieldNameLookup[key] = i;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Serialization attribute removed"));
    }
}
