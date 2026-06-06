// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Licensing;

namespace GauntletCI.Tests;

public class LicenseServiceTests
{
    [Fact]
    public void Load_NonJwtToken_IsRejected()
    {
        const string envVar = "GAUNTLETCI_LICENSE_TEST_NONJWT";
        try
        {
            Environment.SetEnvironmentVariable(envVar, "not-a-jwt-token");
            var license = LicenseService.Load(envVar);

            Assert.False(license.IsValid);
            Assert.False(license.IsLicensed);
            Assert.Equal(LicenseTier.Community, license.Tier);
            Assert.Contains("not a valid signed JWT", license.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void Load_MissingToken_ReturnsCommunity()
    {
        const string envVar = "GAUNTLETCI_LICENSE_TEST_MISSING";
        Environment.SetEnvironmentVariable(envVar, null);

        var license = LicenseService.Load(envVar);

        Assert.True(license.IsValid);
        Assert.False(license.IsLicensed);
        Assert.Equal(LicenseTier.Community, license.Tier);
    }
}
