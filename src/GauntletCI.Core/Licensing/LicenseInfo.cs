// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Licensing;

/// <summary>
/// Represents the result of reading and validating a GauntletCI license token.
/// </summary>
public sealed record LicenseInfo(
    LicenseTier      Tier,
    string?          Email,
    DateTimeOffset?  ExpiresAt,
    bool             IsValid,
    string?          Error = null)
{
    /// <summary>True when the license is valid and grants at least Pro-tier features.</summary>
    public bool IsLicensed => IsValid && Tier >= LicenseTier.Pro;

    /// <summary>True when the token was structurally valid but has passed its expiry date.</summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;

    /// <summary>Returns true when the license grants at least <paramref name="minimum"/> tier access.</summary>
    public bool HasTier(LicenseTier minimum) => IsValid && Tier >= minimum;

    /// <summary>Community (unlicensed) baseline -- no key found.</summary>
    public static LicenseInfo Community => new(LicenseTier.Community, null, null, true);

    /// <summary>Returns an invalid license with the supplied error reason.</summary>
    public static LicenseInfo Invalid(string reason) =>
        new(LicenseTier.Community, null, null, false, reason);

    /// <summary>Returns an expired license record.</summary>
    public static LicenseInfo Expired(DateTimeOffset expiresAt) =>
        new(LicenseTier.Community, null, expiresAt, false,
            $"License expired on {expiresAt:yyyy-MM-dd}. Renew at https://gauntletci.com/pricing");
}
