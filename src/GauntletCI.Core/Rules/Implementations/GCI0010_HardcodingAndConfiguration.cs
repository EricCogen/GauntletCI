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
/// GCI0010, Hardcoding and Configuration
/// Detects hardcoded IPs, URLs, connection strings, ports, and environment names.
/// (Hardcoded credentials/secrets are detected by GCI0012 Security Risk to avoid duplicate findings.)
/// </summary>
public class GCI0010_HardcodingAndConfiguration : RuleBase
{
    public override string Id => "GCI0010";
    public override string Name => "Hardcoding and Configuration";

    // Localhost/private-network patterns that are genuinely hardcoded and environment-specific.
    private static readonly Regex HardcodedUrlRegex =
        new(@"https?://(?:localhost|127\.0\.0\.1|0\.0\.0\.0|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})[:/]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // IP address in a string literal: scoped to literals (not whole line) to avoid matching
    // version strings (1.0.0.0) in XML, NuGet manifests, and comments.
    private static readonly Regex BareIpAddressRegex =
        new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    // Safe-list: public reference URLs that are intentional in code (docs, examples, well-known APIs).
    private static readonly string[] SafeUrlPrefixes =
    [
        "https://docs.microsoft.com", "https://learn.microsoft.com",
        "https://www.nuget.org", "https://nuget.org",
        "https://github.com", "https://raw.githubusercontent.com",
        "https://schema.org", "https://json-schema.org",
        "https://aka.ms", "https://example.com", "http://example.com",
    ];

    private static readonly string[] ConnectionStringMarkers =
        ["Server=", "Data Source=", "mongodb://", "redis://"];

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
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;
            CheckIpAddress(file, findings);
            CheckHardcodedUrl(file, findings);
            CheckConnectionString(file, findings);
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

            // Scope to string literals only: prevents matching version strings like "1.0.0.0"
            // in XML, NuGet manifests, and assembly attributes.
            var literals = ExtractStringLiterals(content);
            if (literals.Count == 0) continue;

            foreach (var literal in literals)
            {
                // Skip if this is a URL: CheckHardcodedUrl already handles that case.
                if (literal.Contains("://", StringComparison.Ordinal)) continue;

                var match = BareIpAddressRegex.Match(literal.Trim());
                if (!match.Success) continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"Hardcoded IP address in string literal: {match.Value}",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Hardcoded IPs break across environments and make infrastructure changes require code changes.",
                    suggestedAction: "Move the IP to configuration (appsettings.json, environment variable, etc.).",
                    confidence: Confidence.Medium));
                break;
            }
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

            // Only fire on localhost/IP URLs: public URLs (docs, CDN, GitHub, etc.) are
            // intentional references, not hardcoded configuration. CheckIpAddress covers
            // the IP-in-URL case; this check adds non-IP localhost specifically.
            bool hasPrivateUrl = literals.Any(l =>
                HardcodedUrlRegex.IsMatch(l) &&
                !SafeUrlPrefixes.Any(s => l.StartsWith(s, StringComparison.OrdinalIgnoreCase)));

            if (!hasPrivateUrl) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded localhost or private-IP URL in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded localhost/IP URLs break across environments and cannot be changed without recompilation.",
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
                if (!literals.Any(l => string.Equals(l, env, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(l, $"ASPNETCORE_ENVIRONMENT={env}", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(l, $"DOTNET_ENVIRONMENT={env}", StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Skip IHostEnvironment fluent calls: IsProduction() etc. are the correct pattern
                if (content.Contains(".IsProduction()", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(".IsStaging()",    StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(".IsDevelopment()", StringComparison.OrdinalIgnoreCase))
                    continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"Hardcoded environment name '{env}' in code.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Hardcoded environment names create fragile branching logic that is hard to test.",
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
