// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        ["Server=", "Data Source=", "mongodb://", "redis://"];

    // Diverges intentionally from WellKnownPatterns.SecretNamePatterns: includes "pwd" (common abbreviation
    // detected in this context) and omits broader entries specific to other rules.
    private static readonly string[] SecretNamePatterns =
        ["password", "secret", "apikey", "api_key", "token", "pwd"];

    private static readonly string[] EnvironmentNames =
        ["production", "staging", "prod"];

    private static readonly int[] KnownPorts = [8080, 3306, 5432, 27017, 6379, 1433, 3000, 8443];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;
            CheckIpAddress(file, findings);
            CheckHardcodedUrl(file, findings);
            CheckConnectionString(file, findings);
            CheckSecrets(file, findings);
            CheckHardcodedPorts(file, findings);
            CheckEnvironmentNames(file, findings);
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckIpAddress(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();

            if (IsCommentLine(trimmed)) continue;
            var match = IpAddressRegex.Match(content);
            if (!match.Success) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Hardcoded IP address detected: {match.Value}",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded IPs break across environments and make infrastructure changes require code changes.",
                suggestedAction: "Move the IP to configuration (appsettings.json, environment variable, etc.).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckHardcodedUrl(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();

            if (IsCommentLine(trimmed)) continue;

            var literals = ExtractStringLiterals(content);
            if (literals.Count == 0) continue;

            bool hasUrlLiteral = literals.Any(l =>
                l.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("https://", StringComparison.OrdinalIgnoreCase));
            if (!hasUrlLiteral) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded URL in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded URLs break across environments and cannot be easily changed without recompilation.",
                suggestedAction: "Move URL to configuration (IConfiguration, environment variable).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckConnectionString(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (IsCommentLine(content.Trim())) continue;
            var literals = ExtractStringLiterals(content);
            if (literals.Count == 0) continue;

            foreach (var marker in ConnectionStringMarkers)
            {
                if (!literals.Any(l => l.Contains(marker, StringComparison.OrdinalIgnoreCase))) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: "Hardcoded connection string detected.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Connection strings in source code expose credentials and prevent per-environment configuration.",
                    suggestedAction: "Use IConfiguration, Secret Manager, or environment variables for connection strings.",
                    confidence: Confidence.High));
                break;
            }
        }
    }

    private void CheckSecrets(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (IsCommentLine(content.Trim())) continue;

            // Require a real assignment (=) not a comparison (==, !=, <=, >=).
            if (!HasAssignment(content)) continue;

            var literals = ExtractStringLiterals(content);
            if (literals.Count == 0) continue;

            // Skip lines where every string literal looks like an env var name (ALL_CAPS_UNDERSCORES).
            // These are references to env var keys, not hardcoded secret values.
            if (literals.All(IsEnvVarName)) continue;

            // Check secret keyword only in the left-hand side of the assignment (the variable name),
            // not anywhere in the line — avoids false positives from type names like HtmlTokenType.
            var eqIndex = FindAssignmentIndex(content);
            var lhs = content[..eqIndex].ToLowerInvariant();

            foreach (var pattern in SecretNamePatterns)
            {
                if (!lhs.Contains(pattern)) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"Possible hardcoded secret ('{pattern}' assigned a string literal).",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Secrets in source code get committed to version control and can leak via logs, diffs, or repo access.",
                    suggestedAction: "Use a secrets manager (Azure Key Vault, AWS Secrets Manager) or environment variables.",
                    confidence: Confidence.High));
                break;
            }
        }
    }

    // Returns true only if the line contains a real assignment = (not ==, !=, <=, >=).
    private static bool HasAssignment(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] != '=') continue;
            char prev = i > 0 ? content[i - 1] : '\0';
            char next = i < content.Length - 1 ? content[i + 1] : '\0';
            if (prev is '!' or '<' or '>' or '=') continue;
            if (next is '=') continue;
            if (prev is '>' && next is '>') continue; // => lambda
            return true;
        }
        return false;
    }

    // Returns the index of the first real assignment = in the line.
    private static int FindAssignmentIndex(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] != '=') continue;
            char prev = i > 0 ? content[i - 1] : '\0';
            char next = i < content.Length - 1 ? content[i + 1] : '\0';
            if (prev is '!' or '<' or '>' or '=') continue;
            if (next is '=') continue;
            return i;
        }
        return content.Length;
    }

    private void CheckHardcodedPorts(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (IsCommentLine(content.Trim())) continue;
            var literals = ExtractStringLiterals(content);

            foreach (var port in KnownPorts)
            {
                if (content.Contains($": {port}") || content.Contains($"Port = {port}") || content.Contains($"port = {port}") ||
                    literals.Any(l => l.Contains($":{port}", StringComparison.Ordinal)))
                {
                    findings.Add(CreateFinding(
                        file,
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

    private void CheckEnvironmentNames(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (IsCommentLine(content.Trim())) continue;
            var literals = ExtractStringLiterals(content);
            if (literals.Count == 0) continue;

            foreach (var env in EnvironmentNames)
            {
                if (!literals.Any(l => l.Contains(env, StringComparison.OrdinalIgnoreCase))) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"Hardcoded environment name '{env}' in code.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Hardcoded environment names create branching logic that is fragile and hard to test.",
                    suggestedAction: "Use IHostEnvironment.IsProduction() or configuration-driven feature flags.",
                    confidence: Confidence.Medium));
                break;
            }
        }
    }

    private static bool IsCommentLine(string trimmed) =>
        trimmed.StartsWith("//", StringComparison.Ordinal) ||
        trimmed.StartsWith("*", StringComparison.Ordinal) ||
        trimmed.StartsWith("#", StringComparison.Ordinal);

    // An env var name is ALL_CAPS with digits and underscores (e.g. GITHUB_TOKEN, MY_API_KEY).
    // Such strings are references to env var keys, not literal secret values.
    private static bool IsEnvVarName(string literal) =>
        literal.Length > 0 && literal.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

    private static List<string> ExtractStringLiterals(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.Contains('"', StringComparison.Ordinal))
            return [];

        try
        {
            var wrapped = $"class __G {{ void __M() {{ {content} }} }}";
            var tree = CSharpSyntaxTree.ParseText(wrapped);
            var root = tree.GetRoot();

            return root.DescendantTokens()
                .Where(t => t.IsKind(SyntaxKind.StringLiteralToken))
                .Select(t => t.ValueText)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;

        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA1054" or "CA1056"))
        {
            findings.Add(CreateFinding(
                summary: $"{diag.Id}: {diag.Message}",
                evidence: $"{diag.FilePath}:{diag.Line}",
                whyItMatters: "URI values represented as raw strings are easy to hardcode incorrectly and harder to validate consistently.",
                suggestedAction: "Prefer Uri-typed APIs and move environment-specific endpoints to configuration.",
                confidence: Confidence.Low));
        }
    }
}
