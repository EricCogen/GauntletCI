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
    public void Load_JsonWithCorpusConfig_DeserializesOllamaUrls()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
                {
                  "corpus": {
                    "ollamaUrls": ["http://localhost:11434", "http://10.0.0.5:11434"],
                    "ollamaModel": "phi3:mini"
                  }
                }
                """;
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), json);

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config.Corpus);
            Assert.Equal(2, config.Corpus.OllamaUrls.Length);
            Assert.Contains("http://localhost:11434", config.Corpus.OllamaUrls);
            Assert.Contains("http://10.0.0.5:11434", config.Corpus.OllamaUrls);
            Assert.Equal("phi3:mini", config.Corpus.OllamaModel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingCorpusSection_ReturnsEmptyOllamaUrls()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gauntletci.json"), "{}");

            var config = ConfigLoader.Load(tempDir);

            Assert.NotNull(config.Corpus);
            Assert.Empty(config.Corpus.OllamaUrls);
            Assert.Null(config.Corpus.OllamaModel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
