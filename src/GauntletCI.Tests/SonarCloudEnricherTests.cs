// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests;

public sealed class SonarCloudEnricherTests
{
    // ── ParseChangedCsFiles ───────────────────────────────────────────────────

    [Fact]
    public async Task ParseChangedCsFiles_ExtractsOnlyCsFiles()
    {
        var diffPath = await CreateTempDiffAsync("""
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,1 @@
            -old
            +new
            diff --git a/src/Bar.ts b/src/Bar.ts
            --- a/src/Bar.ts
            +++ b/src/Bar.ts
            @@ -1,1 +1,1 @@
            -old
            +new
            diff --git a/README.md b/README.md
            --- a/README.md
            +++ b/README.md
            @@ -1,1 +1,1 @@
            -old
            +new
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/Foo.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_StripsB_Prefix()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/deep/Path/MyClass.cs
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/deep/Path/MyClass.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_EmptyDiff_ReturnsEmptySet()
    {
        var diffPath = await CreateTempDiffAsync("");

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_DeduplicatesRepeatedPaths()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Foo.cs
            +++ b/src/Foo.cs
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_IgnoresBinaryFilePlusLines()
    {
        // Binary diff lines do not start with "+++ b/"
        var diffPath = await CreateTempDiffAsync("""
            Binary files a/image.png and b/image.png differ
            +++ b/src/Real.cs
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/Real.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_MultipleFilesReturnedCorrectly()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Alpha.cs
            +++ b/src/Beta.cs
            +++ b/web/app.js
            +++ b/src/Gamma.cs
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Equal(3, result.Count);
        Assert.Contains("src/Alpha.cs", result);
        Assert.Contains("src/Beta.cs", result);
        Assert.Contains("src/Gamma.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_CaseInsensitiveForCsExtension()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Foo.CS
            +++ b/src/Bar.Cs
            """);

        var result = SonarCloudEnricher.ParseChangedCsFiles(diffPath);

        Assert.Equal(2, result.Count);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> CreateTempDiffAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sonar_test_{Guid.NewGuid():N}.patch");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
