// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0010 – Hardcoding and Configuration
/// Detects hardcoded IPs, URLs, connection strings, secrets, and environment names.
/// </summary>
public class GCI0010_HardcodingAndConfiguration : RuleBase
{
    public override string Id => "GCI0010";
    public override string Name => "Hardcoding and Configuration";

    private static readonly Regex IpAddressRegex =
        new(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled);

    private static readonly string[] ConnectionStringMarkers =
        ["\"Server=", "\"Data Source=", "\"mongodb://", "\"redis://"];

    private static readonly string[] SecretNamePatterns =
        ["password", "secret", "apikey", "api_key", "token", "pwd"];

    private static readonly string[] EnvironmentNames =
        ["\"production\"", "\"staging\"", "\"prod\""];

    private static readonly int[] KnownPorts = [8080, 3306, 5432, 27017, 6379, 1433, 3000, 8443];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckIpAddress(diff, findings);
        CheckHardcodedUrl(diff, findings);
        CheckConnectionString(diff, findings);
        CheckSecrets(diff, findings);
        CheckHardcodedPorts(diff, findings);
        CheckEnvironmentNames(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckIpAddress(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();

            // Skip comment lines
            bool isComment = trimmed.StartsWith("//", StringComparison.Ordinal) ||
                              trimmed.StartsWith("*", StringComparison.Ordinal) ||
                              trimmed.StartsWith("#", StringComparison.Ordinal);

            if (isComment) continue;
            var match = IpAddressRegex.Match(content);
            if (!match.Success) continue;

            findings.Add(CreateFinding(
                summary: $"Hardcoded IP address detected: {match.Value}",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded IPs break across environments and make infrastructure changes require code changes.",
                suggestedAction: "Move the IP to configuration (appsettings.json, environment variable, etc.).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckHardcodedUrl(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();

            // Skip comment lines
            bool isComment = trimmed.StartsWith("//", StringComparison.Ordinal) ||
                              trimmed.StartsWith("*", StringComparison.Ordinal) ||
                              trimmed.StartsWith("#", StringComparison.Ordinal);

            if (isComment) continue;
            if (!content.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("https://", StringComparison.OrdinalIgnoreCase)) continue;
            // Only flag string literals
            if (!content.Contains('"')) continue;

            findings.Add(CreateFinding(
                summary: "Hardcoded URL in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded URLs break across environments and cannot be easily changed without recompilation.",
                suggestedAction: "Move URL to configuration (IConfiguration, environment variable).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckConnectionString(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            foreach (var marker in ConnectionStringMarkers)
            {
                if (!content.Contains(marker, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    summary: "Hardcoded connection string detected.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Connection strings in source code expose credentials and prevent per-environment configuration.",
                    suggestedAction: "Use IConfiguration, Secret Manager, or environment variables for connection strings.",
                    confidence: Confidence.High));
                break;
            }
        }
    }

    private void CheckSecrets(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            var lower = content.ToLowerInvariant();
            foreach (var pattern in SecretNamePatterns)
            {
                if (!lower.Contains(pattern)) continue;
                // Must be an assignment with a string literal
                if (!content.Contains('=') || !content.Contains('"')) continue;

                findings.Add(CreateFinding(
                    summary: $"Possible hardcoded secret ('{pattern}' assigned a string literal).",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Secrets in source code get committed to version control and can leak via logs, diffs, or repo access.",
                    suggestedAction: "Use a secrets manager (Azure Key Vault, AWS Secrets Manager) or environment variables.",
                    confidence: Confidence.High));
                break;
            }
        }
    }

    private void CheckHardcodedPorts(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            foreach (var port in KnownPorts)
            {
                if (content.Contains($": {port}") || content.Contains($":{port}\"") ||
                    content.Contains($"Port = {port}") || content.Contains($"port = {port}"))
                {
                    findings.Add(CreateFinding(
                        summary: $"Hardcoded port number {port} detected.",
                        evidence: $"Line {line.LineNumber}: {content.Trim()}",
                        whyItMatters: "Hardcoded ports create conflicts and are inflexible across environments.",
                        suggestedAction: "Externalize port configuration via configuration files or environment variables.",
                        confidence: Confidence.Medium));
                    break;
                }
            }
        }
    }

    private void CheckEnvironmentNames(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var content = line.Content;
            foreach (var env in EnvironmentNames)
            {
                if (!content.Contains(env, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    summary: $"Hardcoded environment name {env} in code.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Hardcoded environment names create branching logic that is fragile and hard to test.",
                    suggestedAction: "Use IHostEnvironment.IsProduction() or configuration-driven feature flags.",
                    confidence: Confidence.Medium));
                break;
            }
        }
    }
}
