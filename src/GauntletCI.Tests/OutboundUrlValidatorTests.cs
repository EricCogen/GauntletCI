// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Security;

namespace GauntletCI.Tests;

public class OutboundUrlValidatorTests
{
    [Theory]
    [InlineData("https://api.example.com/v1/chat/completions")]
    [InlineData("https://mycompany.atlassian.net")]
    public void TryValidateHttpsUrl_AcceptsPublicHosts(string url)
    {
        Assert.True(OutboundUrlValidator.TryValidateHttpsUrl(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://api.example.com/path")]
    [InlineData("https://127.0.0.1/path")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://10.0.0.1/internal")]
    [InlineData("https://192.168.1.1/internal")]
    [InlineData("https://localhost/path")]
    [InlineData("https://[::1]/path")]
    [InlineData("https://[fc00::1]/path")]
    [InlineData("https://[fe80::1]/path")]
    public void TryValidateHttpsUrl_RejectsUnsafeUrls(string url)
    {
        Assert.False(OutboundUrlValidator.TryValidateHttpsUrl(url, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
