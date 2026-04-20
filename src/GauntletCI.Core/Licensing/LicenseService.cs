// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GauntletCI.Core.Licensing;

/// <summary>
/// Reads and validates a GauntletCI license from ~/.gauntletci/gauntletci.key or an env var.
/// Tokens are RS256-signed JWTs. The public key is embedded; no server call is ever made.
/// </summary>
public static class LicenseService
{
    // DEV/PLACEHOLDER key -- replace with your production key pair before issuing live licenses.
    // The matching private key is stored securely in your license issuance service and never committed.
    private static readonly string EmbeddedPublicKey = string.Join("\n",
        "-----BEGIN RSA PUBLIC KEY-----",
        "MIIBCgKCAQEAvAkuG6jEvl90oArpsRl7bf2iaZylvVZtSZKkhhc+E03s1zBBpMlK",
        "GpiOGY1nO2gLPMBfdJbX5xf2yJeJdMvn/v4fNhJJsW9HFwKsRXKHNbcgAD7Bk6Sr",
        "yfePjPbwY+Ax0YlwMlYFCN3/Z+AI6es9oWYCTjAdAZqrVGmTsE7kSv9p1lHuRD4S",
        "vSwPcO3zFkpHbrWdIYDCDRcSpN92CXkVdD/qn1cSe0L1RGqPDQzefXyA/BkBL+aS",
        "rhq3JGRCYEjI05bBby2Lx96jFlB4qqiby3xXwxWXe7tkAUrW9Htadvl/iAl6z9vJ",
        "7ygxKo8oGSSnc2pr9df6HRcoWrSxzzYNjQIDAQAB",
        "-----END RSA PUBLIC KEY-----");

    private static readonly string DefaultLicenseFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "gauntletci.key");

    /// <summary>
    /// Loads license info from the env var or ~/.gauntletci/gauntletci.key.
    /// Returns <see cref="LicenseInfo.Community"/> when no license is found.
    /// </summary>
    /// <param name="envVarName">
    /// Env var to check first. Defaults to GAUNTLETCI_LICENSE; override via config.Llm.LicenseKeyEnv.
    /// </param>
    public static LicenseInfo Load(string envVarName = "GAUNTLETCI_LICENSE")
    {
        var token = ReadToken(envVarName);
        return token is null ? LicenseInfo.Community : Parse(token);
    }

    private static string? ReadToken(string envVarName)
    {
        var envVal = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envVal))
            return envVal.Trim();

        if (File.Exists(DefaultLicenseFilePath))
        {
            try
            {
                var content = File.ReadAllText(DefaultLicenseFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    return content;
            }
            catch { /* unreadable file = no license */ }
        }

        return null;
    }

    private static LicenseInfo Parse(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            // Legacy non-JWT token: backward-compat with the old "any non-empty env var" stub.
            return new LicenseInfo(LicenseTier.Pro, null, null, true,
                "Legacy license key format. Re-issue a signed token at gauntletci.com.");
        }

        try
        {
            var dataToVerify = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature    = Base64UrlDecode(parts[2]);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(EmbeddedPublicKey);

            if (!rsa.VerifyData(dataToVerify, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return LicenseInfo.Invalid(
                    "License signature is invalid. The token may be tampered or was not issued by GauntletCI.");

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc   = JsonDocument.Parse(payloadJson);
            var root        = doc.RootElement;

            if (!root.TryGetProperty("iss", out var iss) || iss.GetString() != "gauntletci.com")
                return LicenseInfo.Invalid("License issuer is invalid.");

            DateTimeOffset? expiresAt = null;
            if (root.TryGetProperty("exp", out var expProp))
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
                if (expiresAt.Value < DateTimeOffset.UtcNow)
                    return LicenseInfo.Expired(expiresAt.Value);
            }

            var tierStr = root.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() : null;
            var tier = tierStr?.ToLowerInvariant() switch
            {
                "pro"        => LicenseTier.Pro,
                "teams"      => LicenseTier.Teams,
                "enterprise" => LicenseTier.Enterprise,
                _            => LicenseTier.Community,
            };

            var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

            return new LicenseInfo(tier, email, expiresAt, true);
        }
        catch (Exception ex)
        {
            return LicenseInfo.Invalid($"License could not be parsed: {ex.Message}");
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = (input.Length % 4) switch
        {
            2 => input + "==",
            3 => input + "=",
            _ => input,
        };
        return Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
    }
}
