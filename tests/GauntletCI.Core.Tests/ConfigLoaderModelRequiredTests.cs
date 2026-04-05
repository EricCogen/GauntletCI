using GauntletCI.Core.Configuration;

namespace GauntletCI.Core.Tests;

public sealed class ConfigLoaderModelRequiredTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "GauntletCI.Tests", Guid.NewGuid().ToString("N"));

    public ConfigLoaderModelRequiredTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadEffective_DefaultsModelRequiredToFalse()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, ".gauntletci.json"), """{ "test_command": "dotnet test" }""");
        ConfigLoader loader = new();

        var config = loader.LoadEffective(_tempDirectory);

        Assert.False(config.ModelRequired);
    }

    [Fact]
    public void LoadEffective_ReadsModelRequiredFromRepoConfig()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, ".gauntletci.json"), """{ "model_required": true }""");
        ConfigLoader loader = new();

        var config = loader.LoadEffective(_tempDirectory);

        Assert.True(config.ModelRequired);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
