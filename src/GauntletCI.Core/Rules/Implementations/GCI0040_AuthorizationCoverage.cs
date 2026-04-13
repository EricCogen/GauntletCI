// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0040 – Authorization Coverage
/// Detects authorization gaps and misconfigurations in ASP.NET Core controllers and middleware.
/// </summary>
public class GCI0040_AuthorizationCoverage : RuleBase
{
    public override string Id => "GCI0040";
    public override string Name => "Authorization Coverage";

    private static readonly string[] AuthAttrs =
        ["[Authorize", "[AllowAnonymous", "[RequirePermission", "[CustomAuthorize"];

    private static readonly string[] WeakJwtSignals =
    [
        "RequireHttpsMetadata = false",
        "ValidateIssuer = false",
        "ValidateAudience = false",
        "ValidateLifetime = false",
        "ValidateIssuerSigningKey = false",
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckUnprotectedController(diff, findings);
        CheckStringRoleLiterals(diff, findings);
        CheckJwtConfigWeakening(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckUnprotectedController(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var path = file.NewPath;

            if (!path.Contains("Controller", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsTestFile(path))
                continue;

            var addedLines = file.AddedLines.ToList();

            bool hasNewAction = addedLines.Any(l =>
                l.Content.Contains("public ", StringComparison.Ordinal) &&
                (l.Content.Contains("ActionResult", StringComparison.Ordinal) ||
                 (l.Content.Contains("Task<", StringComparison.Ordinal) &&
                  l.Content.Contains("Result>", StringComparison.Ordinal))));

            if (!hasNewAction)
                continue;

            bool hasAuthAttr = addedLines.Any(l =>
                AuthAttrs.Any(a => l.Content.Contains(a, StringComparison.Ordinal)));

            if (!hasAuthAttr)
            {
                findings.Add(CreateFinding(
                    summary: "Controller action added without authorization attribute",
                    evidence: $"New public action method detected in {path} without [Authorize], [AllowAnonymous], [RequirePermission], or [CustomAuthorize].",
                    whyItMatters: "Controllers without explicit authorization attributes may be inadvertently accessible to unauthenticated users.",
                    suggestedAction: "Add [Authorize] to protect the endpoint, or [AllowAnonymous] to explicitly mark it as public.",
                    confidence: Confidence.Medium));
            }
        }
    }

    private void CheckStringRoleLiterals(DiffContext diff, List<Finding> findings)
    {
        var matchingLines = diff.AllAddedLines
            .Where(l =>
                (l.Content.Contains("[Authorize(Roles", StringComparison.Ordinal) ||
                 l.Content.Contains("Roles =", StringComparison.Ordinal)) &&
                l.Content.Contains("\"", StringComparison.Ordinal))
            .ToList();

        if (matchingLines.Count == 0)
            return;

        var samples = string.Join("; ", matchingLines.Select(l => l.Content.Trim()).Take(3));

        findings.Add(CreateFinding(
            summary: "Hardcoded role string in authorization attribute",
            evidence: $"Role string literal(s) found: {samples}",
            whyItMatters: "String role literals are error-prone and scatter role names across the codebase. A typo silently grants or denies access.",
            suggestedAction: "Define role names as constants in a central class (e.g. `Roles.Admin`) and reference them from authorization attributes.",
            confidence: Confidence.Medium));
    }

    private void CheckJwtConfigWeakening(DiffContext diff, List<Finding> findings)
    {
        var addedLines = diff.AllAddedLines.ToList();

        foreach (var signal in WeakJwtSignals)
        {
            bool found = addedLines.Any(l =>
                l.Content.Contains(signal, StringComparison.Ordinal));

            if (!found)
                continue;

            findings.Add(CreateFinding(
                summary: "JWT validation weakened",
                evidence: $"Found '{signal}' in added lines.",
                whyItMatters: "Disabling JWT validation checks opens the application to token forgery, replay attacks, and MITM attacks.",
                suggestedAction: "Ensure all JWT validation parameters are enabled in production. Use environment-specific configuration if development relaxation is needed.",
                confidence: Confidence.High));
        }
    }

    private static bool IsTestFile(string path) =>
        path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Spec", StringComparison.OrdinalIgnoreCase);
}
