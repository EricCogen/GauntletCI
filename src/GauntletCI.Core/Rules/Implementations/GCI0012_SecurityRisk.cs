// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0012 – Security Risk
/// Detects SQL injection, weak crypto, dangerous APIs, and credential exposure.
/// </summary>
public class GCI0012_SecurityRisk : RuleBase
{
    public override string Id => "GCI0012";
    public override string Name => "Security Risk";

    private static readonly string[] SqlKeywords = ["\"SELECT ", "\"INSERT ", "\"UPDATE ", "\"DELETE "];
    private static readonly string[] WeakHashAlgorithms = ["MD5.Create()", "SHA1.Create()", "new MD5CryptoServiceProvider", "new SHA1Managed"];
    private static readonly string[] WeakCryptoAlgorithms = ["DESCryptoServiceProvider", "RC2CryptoServiceProvider", "TripleDES"];
    private static readonly string[] DangerousApis = ["Assembly.Load(", "Activator.CreateInstance(", "Process.Start("];
    // Diverges intentionally from WellKnownPatterns.SecretNamePatterns: includes "pwd" and is scoped
    // to patterns relevant to credential assignment checks in this rule.
    private static readonly string[] SecretNamePatterns = ["password", "secret", "apikey", "api_key", "pwd"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var line in diff.AllAddedLines)
        {
            CheckSqlInjection(line, findings);
            CheckWeakHashing(line, findings);
            CheckWeakCrypto(line, findings);
            CheckDangerousApis(line, findings);
            CheckHardcodedCredentials(line, findings);
            CheckInsecureDeserialization(line, findings);
        }

        CheckAllowAnonymousAdded(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckSqlInjection(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;
        bool hasSqlKeyword = SqlKeywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (!hasSqlKeyword) return;

        bool hasConcatenation = content.Contains(" + ", StringComparison.Ordinal) ||
                                 content.Contains("$\"", StringComparison.Ordinal) ||
                                 content.Contains("string.Format", StringComparison.Ordinal);
        if (!hasConcatenation) return;

        findings.Add(CreateFinding(
            summary: "Potential SQL injection: SQL string built via concatenation or interpolation.",
            evidence: $"Line {line.LineNumber}: {content.Trim()}",
            whyItMatters: "String-concatenated SQL is vulnerable to SQL injection attacks that can expose or destroy data.",
            suggestedAction: "Use parameterized queries or an ORM (EF Core, Dapper with parameters).",
            confidence: Confidence.High));
    }

    private void CheckWeakHashing(DiffLine line, List<Finding> findings)
    {
        foreach (var algo in WeakHashAlgorithms)
        {
            if (!line.Content.Contains(algo, StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                summary: $"Weak hashing algorithm used: {algo}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "MD5 and SHA1 are cryptographically broken and must not be used for security purposes.",
                suggestedAction: "Use SHA256, SHA384, or SHA512 via SHA256.Create() etc.",
                confidence: Confidence.High));
            return;
        }
    }

    private void CheckWeakCrypto(DiffLine line, List<Finding> findings)
    {
        foreach (var algo in WeakCryptoAlgorithms)
        {
            if (!line.Content.Contains(algo, StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                summary: $"Weak or deprecated cryptographic algorithm: {algo}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "DES, RC2, and 3DES are deprecated and vulnerable to brute-force attacks.",
                suggestedAction: "Use AES (AesGcm or Aes.Create()) with appropriate key sizes.",
                confidence: Confidence.High));
            return;
        }
    }

    private void CheckDangerousApis(DiffLine line, List<Finding> findings)
    {
        foreach (var api in DangerousApis)
        {
            if (!line.Content.Contains(api, StringComparison.Ordinal)) continue;

            findings.Add(CreateFinding(
                summary: $"Dangerous API call detected: {api}",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Dynamic type loading and process execution with user-controlled input can lead to code injection.",
                suggestedAction: "Validate all arguments, use allowlists, and avoid dynamic execution where possible.",
                confidence: Confidence.High));
            return;
        }
    }

    private void CheckHardcodedCredentials(DiffLine line, List<Finding> findings)
    {
        var lower = line.Content.ToLowerInvariant();
        foreach (var pattern in SecretNamePatterns)
        {
            if (!lower.Contains(pattern)) continue;
            if (!line.Content.Contains('=') || !line.Content.Contains('"')) continue;

            findings.Add(CreateFinding(
                summary: $"Possible hardcoded credential ('{pattern}').",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: "Credentials in source code are exposed via version control and are easily compromised.",
                suggestedAction: "Use a secrets manager or environment variables. Never hardcode credentials.",
                confidence: Confidence.High));
            return;
        }
    }

    private void CheckInsecureDeserialization(DiffLine line, List<Finding> findings)
    {
        var content = line.Content;
        if ((content.Contains("JsonConvert.DeserializeObject", StringComparison.Ordinal) ||
             content.Contains("JsonSerializer.Deserialize", StringComparison.Ordinal)) &&
            (content.Contains("TypeNameHandling.All", StringComparison.Ordinal) ||
             content.Contains("TypeNameHandling.Auto", StringComparison.Ordinal)))
        {
            findings.Add(CreateFinding(
                summary: "Insecure deserialization: TypeNameHandling.All/Auto enables arbitrary type instantiation.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "TypeNameHandling.All/Auto can allow attackers to instantiate arbitrary types via crafted JSON.",
                suggestedAction: "Use TypeNameHandling.None (the default). Implement a custom ISerializationBinder if type discrimination is needed.",
                confidence: Confidence.High));
        }
    }

    private void CheckAllowAnonymousAdded(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            bool hadAuthorize = file.RemovedLines.Any(l =>
                l.Content.Contains("[Authorize", StringComparison.Ordinal));
            bool addedAllowAnonymous = file.AddedLines.Any(l =>
                l.Content.Contains("[AllowAnonymous]", StringComparison.Ordinal));

            if (hadAuthorize && addedAllowAnonymous)
            {
                findings.Add(CreateFinding(
                    summary: $"[AllowAnonymous] added to controller that previously had [Authorize] in {file.NewPath}.",
                    evidence: $"File: {file.NewPath}",
                    whyItMatters: "Removing authorization from a controller bypasses access control and may expose sensitive operations.",
                    suggestedAction: "Verify this is intentional and document the security rationale.",
                    confidence: Confidence.High));
            }
        }
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA2100" or "CA2101" or "CA2153"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0012",
                RuleName = "Security Risk",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a security vulnerability.",
                SuggestedAction = "Address the flagged security issue immediately.",
                Confidence = Confidence.High,
            });
        }
    }
}
