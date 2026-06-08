// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public void Load_ValidToken_WithPublicKeyEnvOverride_AcceptsProTier()
    {
        const string licenseEnv = "GAUNTLETCI_LICENSE_TEST_OVERRIDE";
        const string publicKeyEnv = "GAUNTLETCI_LICENSE_PUBLIC_KEY";
        using var rsa = RSA.Create(2048);
        var publicPem = rsa.ExportRSAPublicKeyPem();
        var token = SignTestJwt(rsa, new { iss = "gauntletci.com", tier = "pro", exp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds() });

        try
        {
            Environment.SetEnvironmentVariable(publicKeyEnv, publicPem);
            Environment.SetEnvironmentVariable(licenseEnv, token);

            var license = LicenseService.Load(licenseEnv);

            Assert.True(license.IsValid);
            Assert.True(license.IsLicensed);
            Assert.Equal(LicenseTier.Pro, license.Tier);
        }
        finally
        {
            Environment.SetEnvironmentVariable(licenseEnv, null);
            Environment.SetEnvironmentVariable(publicKeyEnv, null);
        }
    }

    [Fact]
    public void Load_ValidToken_WithPublicKeyFileOverride_AcceptsProTier()
    {
        const string licenseEnv = "GAUNTLETCI_LICENSE_TEST_FILE";
        const string publicKeyFileEnv = "GAUNTLETCI_LICENSE_PUBLIC_KEY_FILE";
        using var rsa = RSA.Create(2048);
        var publicPem = rsa.ExportRSAPublicKeyPem();
        var token = SignTestJwt(rsa, new { iss = "gauntletci.com", tier = "pro", exp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds() });
        var keyPath = Path.Combine(Path.GetTempPath(), $"gauntletci-test-{Guid.NewGuid():N}.pem");
        File.WriteAllText(keyPath, publicPem);

        try
        {
            Environment.SetEnvironmentVariable(publicKeyFileEnv, keyPath);
            Environment.SetEnvironmentVariable(licenseEnv, token);

            var license = LicenseService.Load(licenseEnv);

            Assert.True(license.IsValid);
            Assert.True(license.IsLicensed);
            Assert.Equal(LicenseTier.Pro, license.Tier);
        }
        finally
        {
            Environment.SetEnvironmentVariable(licenseEnv, null);
            Environment.SetEnvironmentVariable(publicKeyFileEnv, null);
            if (File.Exists(keyPath))
                File.Delete(keyPath);
        }
    }

    private static string SignTestJwt(RSA rsa, object payload)
    {
        var header = Base64UrlEncode("""{"alg":"RS256","typ":"JWT"}""");
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadPart = Base64UrlEncode(payloadJson);
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payloadPart}");
        var signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{payloadPart}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(string value) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
