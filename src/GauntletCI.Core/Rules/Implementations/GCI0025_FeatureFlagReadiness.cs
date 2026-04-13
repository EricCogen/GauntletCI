// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0025 – Feature Flag Readiness
/// Detects large changes to critical-path files that have no feature flag or toggle reference.
/// </summary>
public class GCI0025_FeatureFlagReadiness : RuleBase
{
    public override string Id => "GCI0025";
    public override string Name => "Feature Flag Readiness";

    private const int LargeChangeLinesThreshold = 50;

    private static readonly string[] CriticalPathKeywords =
    [
        "auth", "Auth", "login", "Login", "payment", "Payment",
        "billing", "Billing", "order", "Order", "checkout", "Checkout",
        "subscription", "Subscription", "pricing", "Pricing",
        "token", "Token", "credential", "Credential", "password", "Password"
    ];

    private static readonly string[] FeatureFlagSignals =
    [
        "IFeatureFlag", "FeatureManager", "FeatureFlags", "featureFlag",
        "featureToggle", "FeatureToggle", "IsEnabled", "isEnabled",
        "LaunchDarkly", "Unleash", "flipt", "Flipt",
        "IFeatureManager", "IFeatureVariantManager"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckFeatureFlagReadiness(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckFeatureFlagReadiness(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        if (addedLines.Count < LargeChangeLinesThreshold) return;

        bool isCriticalPath = CriticalPathKeywords.Any(k =>
            file.NewPath.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (!isCriticalPath) return;

        var allContent = file.Hunks.SelectMany(h => h.Lines).Select(l => l.Content);
        bool hasFeatureFlag = allContent.Any(l =>
            FeatureFlagSignals.Any(sig => l.Contains(sig, StringComparison.OrdinalIgnoreCase)));

        if (!hasFeatureFlag)
        {
            findings.Add(CreateFinding(
                summary: $"Large change ({addedLines.Count} lines) to critical-path file {file.NewPath} has no feature flag.",
                evidence: $"{addedLines.Count} lines added to {file.NewPath}",
                whyItMatters: "Large changes to auth, payment, or billing code shipped without a feature flag cannot be rolled back without a full deployment. A flag lets you disable the change instantly if it causes incidents.",
                suggestedAction: "Wrap the new behaviour behind a feature flag (e.g. IFeatureManager.IsEnabledAsync(\"my-flag\")) so it can be disabled without a redeploy.",
                confidence: Confidence.Medium));
        }
    }
}
