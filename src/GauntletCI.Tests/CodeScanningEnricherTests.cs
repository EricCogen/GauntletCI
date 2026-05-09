// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests;

public sealed class CodeScanningEnricherTests
{
    // ── ParseChangedCsFiles ───────────────────────────────────────────────────

    [Fact]
    public async Task ParseChangedCsFiles_ExtractsOnlyCsFiles()
    {
        var diffPath = await CreateTempDiffAsync("""
            diff --git a/src/Foo.cs b/src/Foo.cs
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
            """);

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/Foo.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_StripsB_Prefix()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/deep/Path/MyClass.cs
            """);

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/deep/Path/MyClass.cs", result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_EmptyDiff_ReturnsEmptySet()
    {
        var diffPath = await CreateTempDiffAsync("");

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_DeduplicatesRepeatedPaths()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Foo.cs
            +++ b/src/Foo.cs
            """);

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
    }

    [Fact]
    public async Task ParseChangedCsFiles_IgnoresNonPlusPlusLines()
    {
        var diffPath = await CreateTempDiffAsync("""
            --- a/src/Old.cs
            +++ b/src/New.cs
            Binary files a/image.png and b/image.png differ
            """);

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Single(result);
        Assert.Contains("src/New.cs", result);
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

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

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

        var result = CodeScanningEnricher.ParseChangedCsFiles(diffPath);

        Assert.Equal(2, result.Count);
    }

    // ── CodeScanningAlert model ───────────────────────────────────────────────

    [Fact]
    public void CodeScanningAlert_DefaultValues_AreEmptyNotNull()
    {
        var alert = new CodeScanningAlert();

        Assert.Equal("", alert.Repo);
        Assert.Equal("", alert.FilePath);
        Assert.Equal("", alert.RuleId);
        Assert.Equal("", alert.RuleName);
        Assert.Equal("", alert.Severity);
        Assert.Equal("", alert.State);
        Assert.Equal("", alert.ToolName);
        Assert.Equal("", alert.Message);
        Assert.Equal(0,  alert.StartLine);
    }

    [Fact]
    public void CodeScanningAlert_InitProperties_RoundTrip()
    {
        var alert = new CodeScanningAlert
        {
            Repo      = "dotnet/runtime",
            FilePath  = "src/libraries/Foo.cs",
            RuleId    = "cs/sql-injection",
            RuleName  = "Database query from user input",
            Severity  = "error",
            State     = "open",
            ToolName  = "CodeQL",
            Message   = "This query is vulnerable.",
            StartLine = 42,
        };

        Assert.Equal("dotnet/runtime",                  alert.Repo);
        Assert.Equal("src/libraries/Foo.cs",            alert.FilePath);
        Assert.Equal("cs/sql-injection",                alert.RuleId);
        Assert.Equal("Database query from user input",  alert.RuleName);
        Assert.Equal("error",                           alert.Severity);
        Assert.Equal("open",                            alert.State);
        Assert.Equal("CodeQL",                          alert.ToolName);
        Assert.Equal("This query is vulnerable.",       alert.Message);
        Assert.Equal(42,                                alert.StartLine);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> CreateTempDiffAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cs_test_{Guid.NewGuid():N}.patch");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
