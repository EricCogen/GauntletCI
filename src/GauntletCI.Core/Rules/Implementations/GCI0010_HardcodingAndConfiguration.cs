// SPDX-License-Identifier: Elastic-2.0
using System.Globalization;
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
    public GCI0010_HardcodingAndConfiguration(IPatternProvider patterns) : base(patterns)
    {
    }
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
        ["Server=", "Data Source=", "mongodb://", "redis://", "mysql://", "postgresql://", "Database="];

    private static readonly string[] EnvironmentNames =
        ["production", "staging", "prod", "dev", "sandbox", "development"];

    private static readonly string[] AuthorityHosts =
    [
        "login.microsoftonline.com",
        "login.microsoft.com",
        "login.windows.net",
        "accounts.google.com",
    ];

    private static readonly int[] KnownPorts = [8080, 3306, 5432, 27017, 6379, 1433, 3000, 8443];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;
            if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;
            CheckIpAddress(file, context, findings);
            CheckHardcodedUrl(file, context, findings);
            CheckConnectionString(file, context, findings);
            CheckHardcodedPorts(file, context, findings);
            CheckEnvironmentNames(file, context, findings);
            CheckHardcodedAuthority(file, context, findings);
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckIpAddress(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        // Skip test and infrastructure files - they often have hardcoded localhost/test IPs
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.DependencyInjectionPatterns.IsInfrastructureFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();
            if (WellKnownPatterns.IsCommentLine(trimmed)) continue;

            if (!HasHardcodedLiteral(context, file, line, IsHardcodedIpLiteral))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded IP address in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded IPs break across environments and make infrastructure changes require code changes.",
                suggestedAction: "Move the IP to configuration (appsettings.json, environment variable, etc.).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckHardcodedUrl(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        // Skip test and infrastructure files - they often have hardcoded localhost URLs
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.DependencyInjectionPatterns.IsInfrastructureFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();

            if (WellKnownPatterns.IsCommentLine(trimmed)) continue;

            if (!HasHardcodedLiteral(context, file, line, IsHardcodedPrivateUrlLiteral))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded localhost or private-IP URL in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Hardcoded localhost/IP URLs break across environments and cannot be changed without recompilation.",
                suggestedAction: "Move URL to configuration (IConfiguration, environment variable).",
                confidence: Confidence.Medium));
        }
    }

    private void CheckConnectionString(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        // Phase 17a: GCI0010 ↔ GCI0021 Coordination
        // Skip connection strings in infrastructure/migration files (GCI0021 owns schema context).
        // Connection strings in Migrations/ or Infrastructure/ are typically test/seed data.
        if (WellKnownPatterns.IsInfrastructureFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (WellKnownPatterns.IsCommentLine(content.Trim())) continue;

            if (!HasHardcodedLiteral(context, file, line, ContainsConnectionStringMarker))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded connection string detected.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Connection strings in source code expose credentials and prevent per-environment configuration.",
                suggestedAction: "Use IConfiguration, Secret Manager, or environment variables for connection strings.",
                confidence: Confidence.High));
        }
    }

    private void CheckHardcodedPorts(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (WellKnownPatterns.IsCommentLine(content.Trim())) continue;

            foreach (var port in KnownPorts)
            {
                if (!HasHardcodedPort(context, file, line, port))
                    continue;

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

    private void CheckEnvironmentNames(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (WellKnownPatterns.IsCommentLine(content.Trim())) continue;

            // Skip IHostEnvironment fluent calls: IsProduction() etc. are the correct pattern
            if (content.Contains(".IsProduction()", StringComparison.OrdinalIgnoreCase) ||
                content.Contains(".IsStaging()", StringComparison.OrdinalIgnoreCase) ||
                content.Contains(".IsDevelopment()", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var env in EnvironmentNames)
            {
                if (!HasHardcodedLiteral(context, file, line, literal => MatchesEnvironmentLiteral(literal, env)))
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

    private void CheckHardcodedAuthority(DiffFile file, AnalysisContext context, List<Finding> findings)
    {
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.PerformancePatterns.IsRuleImplementationFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            var trimmed = content.Trim();
            if (WellKnownPatterns.IsCommentLine(trimmed)) continue;
            if (trimmed.Contains("[InlineData(", StringComparison.Ordinal) ||
                trimmed.Contains("[Theory]", StringComparison.Ordinal) ||
                trimmed.Contains("[MemberData(", StringComparison.Ordinal))
                continue;

            if (!HasHardcodedLiteral(context, file, line, IsHardcodedAuthorityLiteral))
                continue;

            findings.Add(CreateFinding(
                file,
                summary: "Hardcoded identity authority URL or host in string literal.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Authority endpoints should come from configuration so tenants and environments can change without recompilation.",
                suggestedAction: "Move authority URL or host list to IConfiguration, OpenIdConnect options, or environment-specific settings.",
                confidence: Confidence.Medium));
        }
    }

    private static bool HasHardcodedLiteral(
        AnalysisContext context,
        DiffFile file,
        DiffLine line,
        Func<string, bool> literalPredicate) =>
        RegexEvidencePromotion.PassesLiteralCandidateValidation(
            context,
            file.NewPath,
            line,
            literalPredicate,
            ExtractStringLiterals);

    private static bool IsHardcodedIpLiteral(string literal)
    {
        if (literal.Contains("://", StringComparison.Ordinal)) return false;
        if (IsLikelyVersionLiteral(literal)) return false;
        return BareIpAddressRegex.IsMatch(literal.Trim());
    }

    private static bool IsHardcodedPrivateUrlLiteral(string literal) =>
        HardcodedUrlRegex.IsMatch(literal) &&
        !SafeUrlPrefixes.Any(s => literal.StartsWith(s, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsConnectionStringMarker(string literal) =>
        ConnectionStringMarkers.Any(marker =>
            literal.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool HasHardcodedPort(AnalysisContext context, DiffFile file, DiffLine line, int port)
    {
        if (context.Syntax is not null)
        {
            return HasHardcodedLiteral(
                context,
                file,
                line,
                literal => literal.Contains($":{port}", StringComparison.Ordinal));
        }

        var content = line.Content;
        var literals = ExtractStringLiterals(content);
        return content.Contains($": {port}", StringComparison.Ordinal)
            || content.Contains($"Port = {port}", StringComparison.Ordinal)
            || content.Contains($"port = {port}", StringComparison.Ordinal)
            || literals.Any(l => l.Contains($":{port}", StringComparison.Ordinal));
    }

    private static bool MatchesEnvironmentLiteral(string literal, string env) =>
        string.Equals(literal, env, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(literal, $"ASPNETCORE_ENVIRONMENT={env}", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(literal, $"DOTNET_ENVIRONMENT={env}", StringComparison.OrdinalIgnoreCase);

    private static bool IsHardcodedAuthorityLiteral(string literal)
    {
        var trimmed = literal.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                return false;

            return MatchesAuthorityHost(uri.Host);
        }

        return MatchesAuthorityHost(trimmed);
    }

    private static bool MatchesAuthorityHost(string host) =>
        AuthorityHosts.Any(authority =>
            host.Equals(authority, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + authority, StringComparison.OrdinalIgnoreCase));

    private static bool IsLikelyVersionLiteral(string literal)
    {
        var trimmed = literal.Trim();
        if (!BareIpAddressRegex.IsMatch(trimmed)) return false;

        var parts = trimmed.Split('.');
        if (parts.Length != 4) return false;
        if (!parts.All(p => int.TryParse(p, out var n) && n >= 0 && n <= 255)) return false;

        return parts.All(p => p.Length <= 2 && int.Parse(p, CultureInfo.InvariantCulture) <= 99);
    }



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

