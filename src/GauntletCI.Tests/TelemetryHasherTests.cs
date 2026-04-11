// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Tests;

public class TelemetryHasherTests
{
    [Fact]
    public void Hash8_EmptyInput_Returns8Zeros()
    {
        Assert.Equal("00000000", TelemetryHasher.Hash8(string.Empty));
    }

    [Fact]
    public void Hash8_NullInput_Returns8Zeros()
    {
        Assert.Equal("00000000", TelemetryHasher.Hash8(null!));
    }

    [Fact]
    public void Hash8_ReturnsExactly8Characters()
    {
        var result = TelemetryHasher.Hash8("https://github.com/owner/repo");
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void Hash8_IsLowerCase()
    {
        var result = TelemetryHasher.Hash8("https://github.com/owner/repo");
        Assert.Equal(result, result.ToLowerInvariant());
    }

    [Fact]
    public void Hash8_IsOnlyHexCharacters()
    {
        var result = TelemetryHasher.Hash8("some-input");
        Assert.Matches("^[0-9a-f]{8}$", result);
    }

    [Fact]
    public void Hash8_IsDeterministic()
    {
        const string input = "https://github.com/EricCogen/GauntletCI";
        Assert.Equal(TelemetryHasher.Hash8(input), TelemetryHasher.Hash8(input));
    }

    [Fact]
    public void Hash8_DifferentInputs_ProduceDifferentHashes()
    {
        var a = TelemetryHasher.Hash8("https://github.com/owner/repo-a");
        var b = TelemetryHasher.Hash8("https://github.com/owner/repo-b");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash8_CaseAndWhitespaceNormalized()
    {
        // Internally does Trim().ToLowerInvariant() before hashing
        Assert.Equal(
            TelemetryHasher.Hash8("GitHub.com/Owner/Repo"),
            TelemetryHasher.Hash8("  github.com/owner/repo  "));
    }

    [Fact]
    public async Task HashRepoAsync_InvalidPath_ReturnsLocal()
    {
        var result = await TelemetryHasher.HashRepoAsync(Path.GetTempPath() + "nonexistent-" + Guid.NewGuid());
        Assert.Equal("local", result);
    }

    [Fact]
    public async Task HashRepoAsync_ValidGitRepo_Returns8CharHexOrLocal()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var result = await TelemetryHasher.HashRepoAsync(repoRoot);
        // Either a valid 8-char hex hash or "local" (no remote configured)
        Assert.True(result == "local" || System.Text.RegularExpressions.Regex.IsMatch(result, "^[0-9a-f]{8}$"),
            $"Unexpected result: {result}");
    }
}
