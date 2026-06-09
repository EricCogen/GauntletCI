// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Licensing;

namespace GauntletCI.Cli.Licensing;

/// <summary>
/// Enforces license tier requirements before paid CLI features run.
/// </summary>
internal static class PaidFeatureGate
{
    /// <summary>
    /// Returns an exit code when the caller must abort; null when execution may continue.
    /// </summary>
    public static Task<int?> TryEnsureTierAsync(
        LicenseTier minimumTier,
        string featureLabel,
        string licenseEnvVar,
        CancellationToken ct = default) =>
        TryEnsureTierAsync(minimumTier > LicenseTier.Community, minimumTier, featureLabel, licenseEnvVar, ct);

    /// <summary>
    /// Returns an exit code when the caller must abort; null when execution may continue.
    /// </summary>
    public static async Task<int?> TryEnsureTierAsync(
        bool usingLicensedFeature,
        LicenseTier minimumTier,
        string featureLabel,
        string licenseEnvVar,
        CancellationToken ct = default)
    {
        if (!usingLicensedFeature)
            return null;

        var license = LicenseService.Load(licenseEnvVar);
        if (!license.HasTier(minimumTier))
        {
            Console.Error.WriteLine(
                $"[GauntletCI] A valid {TierLabel(minimumTier)} license is required for {featureLabel}. " +
                "Set GAUNTLETCI_LICENSE or run: gauntletci license status");
            return 1;
        }

        var rawToken = LicenseService.ReadRawToken(licenseEnvVar);
        if (rawToken is null)
            return 1;

        var netResult = await NetworkLicenseValidator.ValidateAsync(rawToken, ct).ConfigureAwait(false);
        if (!netResult.Valid)
        {
            Console.Error.WriteLine(
                $"[GauntletCI] License subscription is no longer active ({netResult.Reason ?? "cancelled"}). " +
                "Renew at https://gauntletci.com/pricing or run: gauntletci license renew");
            return 1;
        }

        if (netResult.SkippedNetworkCheck)
        {
            if (NetworkLicenseValidator.IsEnterpriseAirGap() && license.HasTier(LicenseTier.Enterprise))
            {
                Console.Error.WriteLine(
                    "[GauntletCI] Using enterprise air-gap offline license mode.");
            }
            else
            {
                Console.Error.WriteLine(
                    "[GauntletCI] Could not verify license subscription online. " +
                    "Renew network access or set GAUNTLETCI_ENTERPRISE_AIRGAP=1 with an Enterprise license.");
                return 1;
            }
        }

        return null;
    }

    /// <summary>Legacy helper: Pro-or-higher with network validation.</summary>
    public static Task<int?> TryEnsureLicensedAsync(
        bool usingLicensedFeature,
        string licenseEnvVar,
        CancellationToken ct = default) =>
        TryEnsureTierAsync(usingLicensedFeature, LicenseTier.Pro, "this feature", licenseEnvVar, ct);

    internal static LicenseTier ComputeAnalyzeRequiredTier(
        bool withLlm,
        bool withExpertCtx,
        bool ghPrComments,
        bool githubChecks,
        bool withTicketCtx,
        bool withCoverage,
        bool notifySlack,
        bool notifyTeams,
        bool useBaseline,
        bool engineeringPolicyEnabled)
    {
        var tier = LicenseTier.Community;

        if (withLlm || withExpertCtx || useBaseline)
            tier = Max(tier, LicenseTier.Pro);

        if (ghPrComments || githubChecks || withTicketCtx || withCoverage
            || notifySlack || notifyTeams || engineeringPolicyEnabled)
            tier = Max(tier, LicenseTier.Teams);

        return tier;
    }

    private static LicenseTier Max(LicenseTier a, LicenseTier b) => a > b ? a : b;

    private static string TierLabel(LicenseTier tier) => tier switch
    {
        LicenseTier.Pro => "Pro-or-higher",
        LicenseTier.Teams => "Teams-or-higher",
        LicenseTier.Enterprise => "Enterprise",
        _ => "Pro-or-higher",
    };
}
