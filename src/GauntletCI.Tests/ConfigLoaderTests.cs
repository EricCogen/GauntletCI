// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;

namespace GauntletCI.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaultConfig()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}");

        var config = ConfigLoader.Load(nonExistentPath);

        Assert.NotNull(config);
        Assert.NotNull(config.Rules);
        Assert.NotNull(config.PolicyReferences);
    }

    [Fact]
    public void Load_EmptyDirectory_ReturnsDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config);
            Assert.NotNull(config.Rules);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ValidJson_DeserializesProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
                {
                  "rules": {
                    "GCI0001": { "enabled": false },
                    "GCI0002": { "enabled": true, "severity": "High" }
                  }
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config);
            Assert.True(config.Rules.ContainsKey("GCI0001"));
            Assert.False(config.Rules["GCI0001"].Enabled);
            Assert.True(config.Rules.ContainsKey("GCI0002"));
            Assert.True(config.Rules["GCI0002"].Enabled);
            Assert.Equal("High", config.Rules["GCI0002"].Severity);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MalformedJson_ReturnsDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), "{{{invalid json");

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config);
            Assert.NotNull(config.Rules);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_EmptyJsonObject_ReturnsDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), "{}");

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config);
            Assert.NotNull(config.Rules);
            Assert.Empty(config.Rules);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_JsonWithTrailingComma_DeserializesWithoutThrowing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // ConfigLoader allows trailing commas via JsonCommentHandling
            var json = """
                {
                  "rules": {
                    "GCI0001": { "enabled": true, },
                  },
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config);
            Assert.True(config.Rules.ContainsKey("GCI0001"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_JsonWithLlmConfig_DeserializesLlmSection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
                {
                  "llm": {
                    "ciEndpoint": "https://api.openai.com/v1/chat/completions",
                    "ciModel": "gpt-4o"
                  }
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config.Llm);
            Assert.Equal("https://api.openai.com/v1/chat/completions", config.Llm.CiEndpoint);
            Assert.Equal("gpt-4o", config.Llm.CiModel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_JsonWithCorpusConfig_DeserializesOllamaEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
                {
                  "corpus": {
                    "ollamaEndpoints": [
                      { "url": "http://localhost:11434", "enabled": true },
                      { "url": "http://10.0.0.5:11434", "enabled": false }
                    ]
                  },
                  "llm": {
                    "model": "phi4-mini:latest"
                  }
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config.Corpus);
            Assert.Equal(2, config.Corpus.OllamaEndpoints.Length);
            Assert.Contains(config.Corpus.OllamaEndpoints, e => e.Url == "http://localhost:11434" && e.Enabled);
            Assert.Contains(config.Corpus.OllamaEndpoints, e => e.Url == "http://10.0.0.5:11434" && !e.Enabled);
            Assert.Equal("phi4-mini:latest", config.Llm?.Model);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_OllamaEndpoint_DefaultsEnabledToTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
                {
                  "corpus": {
                    "ollamaEndpoints": [
                      { "url": "http://localhost:11434" }
                    ]
                  }
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.Single(config.Corpus.OllamaEndpoints);
            Assert.True(config.Corpus.OllamaEndpoints[0].Enabled);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingCorpusSection_ReturnsEmptyOllamaEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), "{}");

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config.Corpus);
            Assert.Empty(config.Corpus.OllamaEndpoints);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
