// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Licensing;

namespace GauntletCI.Cli.Licensing;

/// <summary>
/// Enforces license requirements before paid CLI features run.
/// </summary>
internal static class PaidFeatureGate
{
    /// <summary>
    /// Returns an exit code when the caller must abort; null when execution may continue.
    /// </summary>
    public static async Task<int?> TryEnsureLicensedAsync(
        bool usingLicensedFeature,
        string licenseEnvVar,
        CancellationToken ct = default)
    {
        if (!usingLicensedFeature)
            return null;

        var license = LicenseService.Load(licenseEnvVar);
        if (!license.IsLicensed)
        {
            Console.Error.WriteLine(
                "[GauntletCI] A valid Pro-or-higher license is required for this feature. " +
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
            Console.Error.WriteLine(
                "[GauntletCI] Warning: Could not verify license subscription online. " +
                "Set GAUNTLETCI_ENTERPRISE_AIRGAP=1 to suppress this warning in approved air-gap environments.");
        }

        return null;
    }
}
