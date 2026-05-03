// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.Model;

namespace GauntletCI.Cli.Enrichment.Stages;

/// <summary>
/// Extracts and normalizes the code snippet evidence from findings.
/// Parses evidence (file:line:snippet format) and populates the CodeSnippet field.
/// Always available (no external dependencies).
/// </summary>
public class CodeSnippetEnricher : IFindingEnricher
{
    public string StageName => "CodeSnippet";
    public bool IsAvailable => true;  // Always available, no external deps
    public IReadOnlySet<string> DependsOn => new HashSet<string>();  // No dependencies

    /// <summary>
    /// Extracts code snippet from finding evidence.
    /// Evidence format: "file:line:snippet" or just the snippet.
    /// Skips if already populated.
    /// </summary>
    public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
    {
        if (finding is null)
            return Task.FromResult(false);

        // Skip if already enriched
        if (!string.IsNullOrWhiteSpace(finding.CodeSnippet))
            return Task.FromResult(false);

        // Extract from evidence if available
        if (string.IsNullOrWhiteSpace(finding.Evidence))
            return Task.FromResult(false);

        try
        {
            // Evidence typically contains file:line:snippet
            // Extract just the code part (after the last colon)
            var parts = finding.Evidence.Split(':');
            if (parts.Length >= 3)
            {
                // Last part is usually the snippet
                var snippet = string.Join(":", parts.Skip(2));
                finding.CodeSnippet = snippet.Trim();
                return Task.FromResult(true);
            }
            else if (parts.Length == 1 || parts.Length == 2)
            {
                // Just code without file:line prefix
                finding.CodeSnippet = finding.Evidence.Trim();
                return Task.FromResult(true);
            }
        }
        catch
        {
            // Fall through to skip on error
        }

        return Task.FromResult(false);
    }
}
