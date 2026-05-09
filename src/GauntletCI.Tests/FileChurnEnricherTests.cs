// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests;

public sealed class FileChurnEnricherTests
{
    [Fact]
    public async Task ParseChangedCsFiles_ExtractsCsFilePaths()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Services/UserService.cs
            @@ -1,3 +1,4 @@
            + public class UserService {}
            +++ b/src/Models/User.cs
            @@ -1,2 +1,3 @@
            + public class User {}
            """);

        var result = FileChurnEnricher.ParseChangedCsFiles(diffPath);

        Assert.Equal(2, result.Count);
        Assert.Contains("src/Services/UserService.cs", result);
        Assert.Contains("src/Models/User.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_IgnoresNonCsFiles()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/app.ts
            @@ -1,2 +1,3 @@
            + const x = 1;
            +++ b/README.md
            @@ -1,1 +1,2 @@
            + ## New Section
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
            + public class Foo {}
            """);

        var result = FileChurnEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/Foo.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_EmptyDiff_ReturnsEmpty()
    {
        var diffPath = await CreateTempDiffAsync("");
        var result = FileChurnEnricher.ParseChangedCsFiles(diffPath);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_Deduplicates()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
            + line1
            +++ b/src/Foo.cs
            @@ -10,1 +10,2 @@
            + line2
            """);

        var result = FileChurnEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
    }

    [Theory]
    [InlineData(0,  0.0)]
    [InlineData(15, 0.5)]
    [InlineData(30, 1.0)]
    [InlineData(60, 1.0)]
    public void ComputeHotspotScore_ReturnsExpectedValue(int churn, double expected)
    {
        var score = FileChurnEnricher.ComputeHotspotScore(churn);
        Assert.Equal(expected, score, precision: 5);
    }

    [Fact]
    public void ComputeHotspotScore_ClampsAtOne()
    {
        var score = FileChurnEnricher.ComputeHotspotScore(1000);
        Assert.Equal(1.0, score);
    }

    private static async Task<string> CreateTempDiffAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"churn_test_{Guid.NewGuid():N}.patch");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
