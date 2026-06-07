// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Git;

namespace GauntletCI.Tests;

public class GitRefValidatorTests
{
    [Theory]
    [InlineData("HEAD")]
    [InlineData("main")]
    [InlineData("abc123def456")]
    [InlineData("origin/main")]
    [InlineData("HEAD~1")]
    [InlineData("v1.2.3")]
    [InlineData("refs/heads/main")]
    [InlineData("abc123..def456")]
    public void ValidateRef_AcceptsCommonRefs(string commitRef)
    {
        GitRefValidator.ValidateRef(commitRef);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--output=evil")]
    [InlineData("-n")]
    [InlineData("HEAD;calc")]
    [InlineData("HEAD --output=evil")]
    [InlineData("..main")]
    [InlineData("main..")]
    public void ValidateRef_RejectsUnsafeRefs(string commitRef)
    {
        Assert.Throws<ArgumentException>(() => GitRefValidator.ValidateRef(commitRef));
    }
}
