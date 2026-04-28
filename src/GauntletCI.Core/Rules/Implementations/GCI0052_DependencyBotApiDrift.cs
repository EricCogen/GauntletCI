// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0052, Dependency Bot API Drift
/// Fires when a dependency bot actor (Dependabot, Renovate, Snyk) opens a PR that
/// contains both a lockfile change and a public API method signature change in C# files.
/// </summary>
public class GCI0052_DependencyBotApiDrift : RuleBase
{
    public override string Id   => "GCI0052";
    public override string Name => "Dependency Bot API Drift";

    private static readonly HashSet<string> LockfileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "packages.lock.json",
            "package-lock.json",
            "yarn.lock",
            "Pipfile.lock",
            "go.sum",
            "Cargo.lock",
            "Directory.Packages.props",
        };

    private static readonly HashSet<string> DependencyBotActors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "dependabot[bot]",
            "renovate[bot]",
            "snyk-bot",
            "snyk[bot]",
        };

    // Matches added public method signatures in C# files (Content is already stripped of leading +).
    private static readonly Regex PublicMethodSignatureRegex = new(
        @"^\s*public\s+(static\s+|async\s+|virtual\s+|override\s+|abstract\s+)*[\w<>\[\],]+\s+\w+\s*\(",
        RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        var actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        if (string.IsNullOrEmpty(actor) || !DependencyBotActors.Contains(actor))
            return Task.FromResult(findings);

        // Check all files (eligible and skipped) for lockfile changes
        var allFilePaths = context.EligibleFiles.Select(r => r.FilePath)
            .Concat(context.SkippedFiles.Select(r => r.FilePath));

        bool hasLockfileChange = allFilePaths.Any(path =>
        {
            var fileName = Path.GetFileName(path);
            if (LockfileNames.Contains(fileName)) return true;
            return Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase);
        });

        if (!hasLockfileChange)
            return Task.FromResult(findings);

        // Check for public API changes in CS files
        foreach (var file in context.Diff.Files)
        {
            if (!file.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var line in file.AddedLines)
            {
                if (!PublicMethodSignatureRegex.IsMatch(line.Content))
                    continue;

                findings.Add(CreateFinding(
                    file,
                    summary:         "Dependency bot PR introduces a public API change: verify backward compatibility",
                    evidence:        $"{file.NewPath} line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters:    "Automated dependency bots (Dependabot, Renovate, Snyk) should not be changing public method signatures. This may indicate a transitive dependency pulled in an unexpected API change or a bot misconfiguration.",
                    suggestedAction: "Review the public API change carefully. If unintentional, revert the non-lockfile changes. If intentional, use a human-authored PR instead.",
                    confidence:      Confidence.Medium,
                    line:            line));
            }
        }

        return Task.FromResult(findings);
    }
}
