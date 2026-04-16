// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Tests.Corpus;

public class FixtureIdHelperAdditionalTests
{
    // ── Build ────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_StandardInputs_ReturnsDeterministicId()
    {
        var result = FixtureIdHelper.Build("torvalds", "linux", 4321);
        Assert.Equal("torvalds_linux_pr4321", result);
    }

    [Fact]
    public void Build_UppercaseOwner_LowercasesInOutput()
    {
        var result = FixtureIdHelper.Build("TORVALDS", "linux", 1);
        Assert.Equal("torvalds_linux_pr1", result);
    }

    [Fact]
    public void Build_OwnerWithSlash_SanitizesToUnderscore()
    {
        var result = FixtureIdHelper.Build("my/org", "repo", 5);
        Assert.Equal("my_org_repo_pr5", result);
    }

    [Fact]
    public void Build_OwnerWithBackslash_SanitizesToUnderscore()
    {
        var result = FixtureIdHelper.Build("my\\org", "repo", 5);
        Assert.Equal("my_org_repo_pr5", result);
    }

    [Fact]
    public void Build_OwnerWithSpace_SanitizesToDash()
    {
        var result = FixtureIdHelper.Build("my org", "repo", 5);
        Assert.Equal("my-org_repo_pr5", result);
    }

    [Fact]
    public void Build_ZeroPrNumber_IncludesPr0()
    {
        var result = FixtureIdHelper.Build("a", "b", 0);
        Assert.Equal("a_b_pr0", result);
    }

    // ── GetFixturePath ───────────────────────────────────────────────────────

    [Fact]
    public void GetFixturePath_Silver_ReturnsCorrectPath()
    {
        var result = FixtureIdHelper.GetFixturePath(@"C:\corpus", FixtureTier.Silver, "foo_bar_pr1");
        Assert.Equal(Path.Combine(@"C:\corpus", "silver", "foo_bar_pr1"), result);
    }

    [Fact]
    public void GetFixturePath_Discovery_ReturnsCorrectPath()
    {
        var result = FixtureIdHelper.GetFixturePath("/data", FixtureTier.Discovery, "x_y_pr2");
        Assert.Equal(Path.Combine("/data", "discovery", "x_y_pr2"), result);
    }

    // ── GetRawPath ───────────────────────────────────────────────────────────

    [Fact]
    public void GetRawPath_ReturnsRawSubfolder()
    {
        var fixturePath = @"C:\corpus\silver\foo_bar_pr1";
        var result = FixtureIdHelper.GetRawPath(fixturePath);
        Assert.Equal(Path.Combine(fixturePath, "raw"), result);
    }
}
