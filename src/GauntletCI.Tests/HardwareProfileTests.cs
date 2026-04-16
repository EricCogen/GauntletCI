// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;

namespace GauntletCI.Tests;

public class HardwareProfileTests
{
    private static long Gb(long gb) => gb * 1_073_741_824L;

    // ── Apple Silicon branch ─────────────────────────────────────────────────

    [Fact]
    public void RecommendedModel_AppleSilicon_32PlusGbRam_ReturnsLlama3()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true, TotalRamBytes = Gb(34) };
        Assert.Equal("llama3", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_AppleSilicon_16GbRam_ReturnsMistral()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true, TotalRamBytes = Gb(17) };
        Assert.Equal("mistral", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_AppleSilicon_8GbRam_ReturnsPhi3Mini()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true, TotalRamBytes = Gb(8) };
        Assert.Equal("phi3:mini", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_AppleSilicon_LessThan8GbRam_ReturnsTinyLlama()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true, TotalRamBytes = Gb(4) };
        Assert.Equal("tinyllama", profile.RecommendedModel);
    }

    // ── Dedicated GPU branch ─────────────────────────────────────────────────

    [Fact]
    public void RecommendedModel_DedicatedGpu_10PlusGbVram_ReturnsLlama3()
    {
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = Gb(10) };
        Assert.Equal("llama3", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_DedicatedGpu_6GbVram_ReturnsMistral()
    {
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = Gb(6) };
        Assert.Equal("mistral", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_DedicatedGpu_4GbVram_ReturnsPhi3Mini()
    {
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = Gb(4) };
        Assert.Equal("phi3:mini", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_DedicatedGpu_LessThan4GbVram_ReturnsTinyLlama()
    {
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = Gb(2) };
        Assert.Equal("tinyllama", profile.RecommendedModel);
    }

    // ── CPU-only branch ──────────────────────────────────────────────────────

    [Fact]
    public void RecommendedModel_CpuOnly_11GbRam_ReturnsMistral()
    {
        // usable = 11 - 3 = 8 → "mistral"
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = 0, TotalRamBytes = Gb(11) };
        Assert.Equal("mistral", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_CpuOnly_7GbRam_ReturnsPhi3Mini()
    {
        // usable = 7 - 3 = 4 → "phi3:mini"
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = 0, TotalRamBytes = Gb(7) };
        Assert.Equal("phi3:mini", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_CpuOnly_5GbRam_ReturnsTinyLlama()
    {
        // usable = 5 - 3 = 2 → "tinyllama"
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = 0, TotalRamBytes = Gb(5) };
        Assert.Equal("tinyllama", profile.RecommendedModel);
    }

    [Fact]
    public void RecommendedModel_CpuOnly_4GbRam_ReturnsTinyLlama()
    {
        // usable = 4 - 3 = 1 → "tinyllama"
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = 0, TotalRamBytes = Gb(4) };
        Assert.Equal("tinyllama", profile.RecommendedModel);
    }

    // ── Computed properties ──────────────────────────────────────────────────

    [Fact]
    public void TotalRamGb_CalculatesCorrectly()
    {
        var profile = new HardwareProfile { TotalRamBytes = 1_073_741_824L };
        Assert.Equal(1.0, profile.TotalRamGb, precision: 5);
    }

    [Fact]
    public void GpuVramGb_CalculatesCorrectly()
    {
        var profile = new HardwareProfile { GpuVramBytes = 2_147_483_648L };
        Assert.Equal(2.0, profile.GpuVramGb, precision: 5);
    }

    [Fact]
    public void HasGpuAcceleration_WithVram_ReturnsTrue()
    {
        var profile = new HardwareProfile { GpuVramBytes = Gb(4) };
        Assert.True(profile.HasGpuAcceleration);
    }

    [Fact]
    public void HasGpuAcceleration_AppleSilicon_ReturnsTrue()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true, GpuVramBytes = 0 };
        Assert.True(profile.HasGpuAcceleration);
    }

    [Fact]
    public void HasGpuAcceleration_NeitherGpuNorApple_ReturnsFalse()
    {
        var profile = new HardwareProfile { GpuVramBytes = 0, IsAppleSilicon = false };
        Assert.False(profile.HasGpuAcceleration);
    }

    // ── ToSummaryString ──────────────────────────────────────────────────────

    [Fact]
    public void ToSummaryString_ContainsRamAndCores()
    {
        var profile = new HardwareProfile { TotalRamBytes = Gb(16), CpuCores = 8 };
        var summary = profile.ToSummaryString();
        Assert.Contains("RAM", summary);
        Assert.Contains("cores", summary);
    }

    [Fact]
    public void ToSummaryString_NoGpu_ContainsNoGpuDetected()
    {
        var profile = new HardwareProfile { IsAppleSilicon = false, GpuVramBytes = 0 };
        var summary = profile.ToSummaryString();
        Assert.Contains("no GPU detected", summary);
    }

    [Fact]
    public void ToSummaryString_AppleSilicon_ContainsAppleSilicon()
    {
        var profile = new HardwareProfile { IsAppleSilicon = true };
        var summary = profile.ToSummaryString();
        Assert.Contains("Apple Silicon", summary);
    }
}
