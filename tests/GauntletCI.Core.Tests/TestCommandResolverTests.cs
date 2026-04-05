using GauntletCI.Core.Configuration;

namespace GauntletCI.Core.Tests;

public sealed class TestCommandResolverTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "GauntletCI.Tests", Guid.NewGuid().ToString("N"));

    public TestCommandResolverTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Resolve_UsesConfiguredCommand_WhenProvided()
    {
        TestCommandResolver resolver = new();

        string command = resolver.Resolve(_tempDirectory, "npm test");

        Assert.Equal("npm test", command);
    }

    [Fact]
    public void Resolve_DetectsDotnetSolution()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "Demo.sln"), "");
        TestCommandResolver resolver = new();

        string command = resolver.Resolve(_tempDirectory, null);

        Assert.Equal("dotnet test", command);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
