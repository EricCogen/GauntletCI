// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0042 – Package Dependency Changes
/// Detects risky NuGet package dependency changes in .csproj files:
/// new package additions, suspicious/typosquatted names, and version downgrades.
/// Only evaluates files whose path ends with .csproj.
/// </summary>
[ArchivedRule("Supply chain change, not code change")]
public class GCI0042_PackageDependencyChanges : RuleBase
{
    public override string Id => "GCI0042";
    public override string Name => "Package Dependency Changes";

    private static readonly Regex PackageNameRegex =
        new(@"Include=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PackageVersionRegex =
        new(@"Version=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SuspiciousPatterns =
    [
        "Microsft.", "Mircosoft.", "Newtonsoft.Json2", "l0gg", "l0g", "-malware", "-hack", "-crack"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (!file.NewPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                continue;

            CheckNewPackageAdditions(file, findings);
            CheckSuspiciousPackageNames(file, findings);
            CheckVersionDowngrades(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckNewPackageAdditions(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            if (!line.Content.Contains("<PackageReference Include=", StringComparison.OrdinalIgnoreCase))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: "New NuGet package reference added",
                evidence: line.Content.Trim(),
                whyItMatters: "New dependencies introduce supply chain risk, license obligations, and potential transitive dependency conflicts.",
                suggestedAction: "Verify the package is from a trusted publisher, review its license, and check for known vulnerabilities with `dotnet list package --vulnerable`.",
                confidence: Confidence.Low,
                line: line));
        }
    }

    private void CheckSuspiciousPackageNames(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            if (!line.Content.Contains("<PackageReference Include=", StringComparison.OrdinalIgnoreCase))
                continue;

            var nameMatch = PackageNameRegex.Match(line.Content);
            if (!nameMatch.Success) continue;

            var packageName = nameMatch.Groups[1].Value;
            foreach (var pattern in SuspiciousPatterns)
            {
                if (!packageName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Suspicious package name — possible typosquatting",
                    evidence: $"Package '{packageName}' matches suspicious pattern '{pattern}'",
                    whyItMatters: "Typosquatted packages are a common supply chain attack vector that can execute arbitrary code during build or at runtime.",
                    suggestedAction: "Verify the exact package name on nuget.org. Reject this change if the package cannot be verified.",
                    confidence: Confidence.High,
                    line: line));
                break;
            }
        }
    }

    private void CheckVersionDowngrades(DiffFile file, List<Finding> findings)
    {
        var removedVersions = ExtractPackageVersions(file.RemovedLines);
        var addedVersions = ExtractPackageVersions(file.AddedLines);

        foreach (var (packageName, addedVersion) in addedVersions)
        {
            if (!removedVersions.TryGetValue(packageName, out var removedVersion)) continue;

            if (removedVersion > addedVersion)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: "Package version downgraded",
                    evidence: $"'{packageName}' downgraded from {removedVersion} to {addedVersion}",
                    whyItMatters: "Downgrading a package version may reintroduce known vulnerabilities that were fixed in the higher version.",
                    suggestedAction: "Confirm this downgrade is intentional and verify the lower version is not affected by CVEs fixed in the higher version.",
                    confidence: Confidence.Medium));
            }
        }
    }

    private static Dictionary<string, Version> ExtractPackageVersions(IEnumerable<DiffLine> lines)
    {
        var result = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (!line.Content.Contains("<PackageReference Include=", StringComparison.OrdinalIgnoreCase))
                continue;

            var nameMatch = PackageNameRegex.Match(line.Content);
            if (!nameMatch.Success) continue;

            var versionMatch = PackageVersionRegex.Match(line.Content);
            if (!versionMatch.Success) continue;

            if (Version.TryParse(versionMatch.Groups[1].Value, out var version))
                result[nameMatch.Groups[1].Value] = version;
        }

        return result;
    }
}
