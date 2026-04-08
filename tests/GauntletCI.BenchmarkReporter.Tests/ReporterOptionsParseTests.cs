namespace GauntletCI.BenchmarkReporter.Tests;

public sealed class ReporterOptionsParseTests
{
    [Fact]
    public void Parse_UsesDefaults_WhenNoArgumentsAreProvided()
    {
        ReporterOptions options = ReporterOptions.Parse([]);

        Assert.Equal(Path.Combine(options.RepoRoot, "tests", "GauntletCI.Benchmarks", "Fixtures", "curated"), options.FixturesRoot);
        Assert.Equal(Path.Combine(options.RepoRoot, "docs", "benchmarks"), options.OutputDirectory);
        Assert.False(options.IncludeSynthetic);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_RecognizesExplicitOptions()
    {
        ReporterOptions options = ReporterOptions.Parse([
            "--repo-root", ".",
            "--fixtures-root", "fixtures",
            "--output-dir", "out",
            "--include-synthetic"]);

        Assert.Equal(Path.GetFullPath("."), options.RepoRoot);
        Assert.Equal(Path.GetFullPath("fixtures"), options.FixturesRoot);
        Assert.Equal(Path.GetFullPath("out"), options.OutputDirectory);
        Assert.True(options.IncludeSynthetic);
        Assert.False(options.ShowHelp);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_SetsShowHelp_WhenHelpIsRequested(string helpArg)
    {
        ReporterOptions options = ReporterOptions.Parse([helpArg]);

        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_SetsShowHelp_ForUnknownArgument()
    {
        ReporterOptions options = ReporterOptions.Parse(["--unknown"]);

        Assert.True(options.ShowHelp);
    }
}
