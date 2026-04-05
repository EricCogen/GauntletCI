namespace GauntletCI.Cli.Tests;

public class UnitTest1
{
    [Fact]
    public void Parse_RecognizesInstallCommand()
    {
        CliOptions options = CliOptions.Parse(["install"]);

        Assert.Equal("install", options.Command);
        Assert.False(options.FullMode);
    }

    [Fact]
    public void Parse_RecognizesFormatAndRule()
    {
        CliOptions options = CliOptions.Parse(["--format", "json", "--rule", "FL005", "--fast", "--full", "--no-telemetry"]);

        Assert.Equal("review", options.Command);
        Assert.True(options.JsonOutput);
        Assert.Equal("FL005", options.Rule);
        Assert.True(options.FastMode);
        Assert.True(options.FullMode);
        Assert.True(options.NoTelemetry);
    }
}