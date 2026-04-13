// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0020 – Accountability Standard
/// Checks for embarrassing patterns: swallowed exceptions, secrets, commented-out code, empty roles.
/// </summary>
public class GCI0020_AccountabilityStandard : RuleBase
{
    public override string Id => "GCI0020";
    public override string Name => "Accountability Standard";

    // Diverges intentionally from WellKnownPatterns.SecretNamePatterns: includes "pwd" and "passwd"
    // as abbreviations relevant to this accountability check, and excludes broader infrastructure terms.
    private static readonly string[] SecretPatterns = ["password", "secret", "apikey", "api_key", "pwd", "passwd"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckSecretsInCode(diff, findings);
        CheckCommentedOutCodeBlocks(diff, findings);
        CheckEmptyRoleAuthorization(diff, findings);
        CheckUnreachableCode(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckSecretsInCode(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            var lower = line.Content.ToLowerInvariant();
            foreach (var pattern in SecretPatterns)
            {
                if (!lower.Contains(pattern)) continue;
                if (!line.Content.Contains('=') || !line.Content.Contains('"')) continue;

                findings.Add(CreateFinding(
                    summary: $"Literal credential pattern '{pattern}' assigned a string value.",
                    evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters: "Credentials in code will be in version control forever, even after removal.",
                    suggestedAction: "Use a secrets manager. This should block the PR unconditionally.",
                    confidence: Confidence.High));
                return;
            }
        }
    }

    private void CheckCommentedOutCodeBlocks(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();
            int consecutiveComments = 0;
            int startLine = 0;

            foreach (var line in addedLines)
            {
                var trimmed = line.Content.Trim();
                bool isXmlDoc = trimmed.StartsWith("///", StringComparison.Ordinal);
                bool isLineComment = !isXmlDoc && trimmed.StartsWith("//", StringComparison.Ordinal);
                if (isLineComment)
                {
                    if (consecutiveComments == 0) startLine = line.LineNumber;
                    consecutiveComments++;
                }
                else
                {
                    if (consecutiveComments >= 5)
                    {
                        findings.Add(CreateFinding(
                            file,
                            summary: $"Block of {consecutiveComments} consecutive comment lines in {file.NewPath} — possible commented-out code.",
                            evidence: $"Starting at line {startLine} in {file.NewPath}",
                            whyItMatters: "Commented-out code is dead weight that confuses readers and suggests incomplete cleanup.",
                            suggestedAction: "Remove commented-out code. If needed for reference, use version control history instead.",
                            confidence: Confidence.Medium,
                            line: line));
                        break;
                    }
                    consecutiveComments = 0;
                }
            }
        }
    }

    private void CheckEmptyRoleAuthorization(DiffContext diff, List<Finding> findings)
    {
        foreach (var line in diff.AllAddedLines)
        {
            if (line.Content.Contains("[Authorize(Roles = \"\")]", StringComparison.Ordinal) ||
                line.Content.Contains("[Authorize(Roles=\"\")]", StringComparison.Ordinal))
            {
                findings.Add(CreateFinding(
                    summary: "Empty Roles string in [Authorize(Roles = \"\")] — authorization policy is effectively empty.",
                    evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters: "An empty Roles attribute may not restrict access as intended, bypassing authorization.",
                    suggestedAction: "Specify explicit roles or use [Authorize(Policy = \"...\")] with a proper policy.",
                    confidence: Confidence.High));
                return;
            }
        }
    }

    private void CheckUnreachableCode(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count - 1; i++)
            {
                var content = addedLines[i].Content.Trim();
                bool isTerminator = content.StartsWith("return ", StringComparison.Ordinal) ||
                                    content == "return;" ||
                                    content.StartsWith("throw ", StringComparison.Ordinal);
                if (!isTerminator) continue;

                var next = addedLines[i + 1].Content.Trim();
                if (!string.IsNullOrWhiteSpace(next) && next != "}" && next != "{" &&
                    !next.StartsWith("//", StringComparison.Ordinal) &&
                    !next.StartsWith("case ", StringComparison.Ordinal) &&
                    !next.StartsWith("default:", StringComparison.Ordinal))
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Possible unreachable code after return/throw in {file.NewPath}.",
                        evidence: $"Line {addedLines[i].LineNumber}: {content} → Line {addedLines[i + 1].LineNumber}: {next}",
                        whyItMatters: "Unreachable code indicates a logic error or dead code that will never execute.",
                        suggestedAction: "Remove the unreachable code or fix the logic so it can actually be reached.",
                        confidence: Confidence.High));
                    break;
                }
            }
        }
    }
}
