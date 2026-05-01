using System.Text.Json;
using Microsoft.Extensions.Logging;
using GauntletCI.Watchtower.Data;
using GauntletCI.Watchtower.Models.Entities;

namespace GauntletCI.Watchtower.Services;

public class ValidationExecutorImpl : IValidationExecutor
{
    private readonly ILogger<ValidationExecutorImpl> _logger;
    private readonly GauntletCommandRunner _commandRunner;

    public ValidationExecutorImpl(ILogger<ValidationExecutorImpl> logger, GauntletCommandRunner commandRunner)
    {
        _logger = logger;
        _commandRunner = commandRunner;
    }

    public async Task<(bool Success, string Stdout, string Stderr, int ExitCode)> ValidateAsync(string diffPath, string cliPath)
    {
        if (!File.Exists(diffPath))
        {
            return (false, string.Empty, $"Diff file not found: {diffPath}", -1);
        }

        if (!File.Exists(cliPath))
        {
            return (false, string.Empty, $"CLI executable not found: {cliPath}", -1);
        }

        _logger.LogInformation("Running validation with CLI {CliPath} against diff {DiffPath}", cliPath, diffPath);

        var cliDir = Path.GetDirectoryName(cliPath) ?? ".";
        var cliName = Path.GetFileName(cliPath);

        // Build command: GauntletCI.Cli verify --diff <path> --format json
        var arguments = $"verify --diff \"{diffPath}\" --format json";

        var result = await _commandRunner.RunCommandAsync(cliName, arguments, cliDir, 300);

        _logger.LogInformation("Validation completed with exit code {ExitCode}", result.ExitCode);

        return result;
    }
}

public class ResultParserImpl : IResultParser
{
    private readonly ILogger<ResultParserImpl> _logger;

    public ResultParserImpl(ILogger<ResultParserImpl> logger)
    {
        _logger = logger;
    }

    public (bool Detected, float Confidence, List<string> TriggeredRules) ParseResults(string jsonOutput)
    {
        _logger.LogInformation("Parsing validation results");

        try
        {
            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                _logger.LogWarning("Empty JSON output provided");
                return (false, 0f, new List<string>());
            }

            // Parse JSON
            using (JsonDocument doc = JsonDocument.Parse(jsonOutput))
            {
                var root = doc.RootElement;

                // Try to extract detection info
                // Expected JSON structure from GauntletCI:
                // {
                //   "findings": [
                //     {
                //       "ruleId": "GCI0001",
                //       "severity": "high",
                //       "confidence": 0.95,
                //       ...
                //     }
                //   ]
                // }

                var detected = false;
                var maxConfidence = 0f;
                var triggeredRules = new List<string>();

                if (root.TryGetProperty("findings", out var findingsElement) && findingsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var finding in findingsElement.EnumerateArray())
                    {
                        detected = true;

                        if (finding.TryGetProperty("ruleId", out var ruleId))
                        {
                            var rule = ruleId.GetString();
                            if (!string.IsNullOrEmpty(rule) && !triggeredRules.Contains(rule))
                            {
                                triggeredRules.Add(rule);
                            }
                        }

                        if (finding.TryGetProperty("confidence", out var confidence) && confidence.TryGetSingle(out var confValue))
                        {
                            maxConfidence = Math.Max(maxConfidence, confValue);
                        }
                    }
                }

                _logger.LogInformation("Parsed results: Detected={Detected}, Confidence={Confidence}, Rules={RuleCount}",
                    detected, maxConfidence, triggeredRules.Count);

                return (detected, maxConfidence, triggeredRules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing validation results");
            return (false, 0f, new List<string>());
        }
    }
}

public class ArticleGeneratorServiceImpl : IArticleGeneratorService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<ArticleGeneratorServiceImpl> _logger;

    public ArticleGeneratorServiceImpl(WatchtowerDbContext dbContext, ILogger<ArticleGeneratorServiceImpl> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ArticleDraft> GenerateArticleDraftAsync(ValidationResult result, CVE cve)
    {
        _logger.LogInformation("Generating article draft for CVE {CveId}", cve.CveId);

        var draftBody = GenerateDraftContent(cve, result);

        var draft = new ArticleDraft
        {
            CveId = cve.CveId,
            ValidationResultId = result.Id,
            DraftBody = draftBody,
            ReviewNotes = "Article auto-generated by Watchtower. Please review before publication."
        };

        _dbContext.ArticleDrafts.Add(draft);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Article draft created with ID {DraftId}", draft.Id);

        return draft;
    }

    private string GenerateDraftContent(CVE cve, ValidationResult result)
    {
        var rulesTriggered = result.TriggeredRules.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var content = new System.Text.StringBuilder();

        content.AppendLine($"# GauntletCI Detected: {cve.CveId}");
        content.AppendLine();
        content.AppendLine($"**Title:** {cve.Title}");
        content.AppendLine($"**CVSS Score:** {cve.CvssScore}");
        content.AppendLine($"**Severity:** {cve.Severity}");
        content.AppendLine();
        content.AppendLine("## Detection Summary");
        content.AppendLine($"The latest version of GauntletCI successfully detected the behavioral pattern associated with {cve.CveId}.");
        content.AppendLine();
        content.AppendLine("## Rules Triggered");
        content.AppendLine();
        foreach (var rule in rulesTriggered)
        {
            content.AppendLine($"- {rule}");
        }
        content.AppendLine();
        content.AppendLine($"**Confidence Score:** {result.ConfidenceScore:P1}");
        content.AppendLine();
        content.AppendLine("## What This Means");
        content.AppendLine("This detection demonstrates GauntletCI's capability to identify real-world behavioral risks that lead to security vulnerabilities. The tool correctly identified the pattern and flagged it with high confidence.");
        content.AppendLine();
        content.AppendLine("## Additional Resources");
        content.AppendLine($"- [NVD Entry]({cve.SourceUrl})");
        content.AppendLine();
        content.AppendLine("---");
        content.AppendLine("_Article auto-generated by Project Watchtower_");

        return content.ToString();
    }
}

public class GapAnalyzerServiceImpl : IGapAnalyzerService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<GapAnalyzerServiceImpl> _logger;

    public GapAnalyzerServiceImpl(WatchtowerDbContext dbContext, ILogger<GapAnalyzerServiceImpl> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Miss> AnalyzeMissAsync(ValidationResult result, CVE cve)
    {
        _logger.LogInformation("Analyzing miss for CVE {CveId}", cve.CveId);

        var reason = AnalyzeMissReason(cve, result);
        var suggestions = GenerateSuggestions(cve);

        var miss = new Miss
        {
            CveId = cve.CveId,
            ValidationResultId = result.Id,
            ReasonForMiss = reason,
            SuggestedRuleEnhancements = suggestions
        };

        _dbContext.Misses.Add(miss);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Miss analysis created with ID {MissId}", miss.Id);

        return miss;
    }

    private string AnalyzeMissReason(CVE cve, ValidationResult result)
    {
        var reasons = new List<string>();

        // Analyze why detection failed
        if (string.IsNullOrEmpty(result.CapturedDiff))
        {
            reasons.Add("Insufficient diff context provided");
        }

        if (cve.CvssScore >= 9.0)
        {
            reasons.Add("Critical severity CVE - may require specialized detection logic");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Detection pattern not recognized by current rules");
        }

        return string.Join("; ", reasons);
    }

    private string GenerateSuggestions(CVE cve)
    {
        var suggestions = new List<string>();

        if (cve.CvssScore >= 9.0)
        {
            suggestions.Add("Create dedicated rule for critical-severity behavioral patterns");
        }

        suggestions.Add("Review advisory for additional context clues that could inform new detection rules");
        suggestions.Add("Analyze false negative patterns to improve rule precision");

        return string.Join("; ", suggestions);
    }
}

public class ArticlePublisherServiceImpl : IArticlePublisherService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<ArticlePublisherServiceImpl> _logger;

    public ArticlePublisherServiceImpl(WatchtowerDbContext dbContext, ILogger<ArticlePublisherServiceImpl> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> PublishArticleAsync(ArticleDraft draft)
    {
        _logger.LogInformation("Publishing article for CVE {CveId}", draft.CveId);

        try
        {
            // Create published article
            var article = new Article
            {
                CveId = draft.CveId,
                ValidationResultId = draft.ValidationResultId,
                GauntletVersionTested = "1.0.0", // TODO: Get from actual version
                ArticleBody = draft.DraftBody,
                RulesFired = "", // TODO: Extract from draft
                ConfidenceScores = 0f, // TODO: Extract from result
                PublicationDate = DateTime.UtcNow,
                ArticleUrl = $"/blog/{draft.CveId.ToLower()}" // TODO: Generate proper URL
            };

            _dbContext.Articles.Add(article);

            // Mark draft as reviewed
            draft.ReviewedAt = DateTime.UtcNow;
            _dbContext.ArticleDrafts.Update(draft);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Article published with ID {ArticleId}", article.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish article");
            return false;
        }
    }
}

public class RunSummaryServiceImpl : IRunSummaryService
{
    private readonly ILogger<RunSummaryServiceImpl> _logger;

    public RunSummaryServiceImpl(ILogger<RunSummaryServiceImpl> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateSummaryAsync(ValidationRun run)
    {
        _logger.LogInformation("Generating summary for validation run {RunId}", run.Id);

        var detectionRate = run.TotalAnalyzed > 0 ? ((float)run.Detected / run.TotalAnalyzed * 100) : 0;

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("# Watchtower Validation Run Summary");
        summary.AppendLine();
        summary.AppendLine($"**Run ID:** {run.Id}");
        summary.AppendLine($"**Start Time:** {run.StartTime:O}");
        summary.AppendLine($"**End Time:** {run.EndTime:O}");
        summary.AppendLine($"**Status:** {run.Status}");
        summary.AppendLine();
        summary.AppendLine("## Results");
        summary.AppendLine();
        summary.AppendLine($"- **Total CVEs Analyzed:** {run.TotalAnalyzed}");
        summary.AppendLine($"- **Detected:** {run.Detected}");
        summary.AppendLine($"- **Missed:** {run.Missed}");
        summary.AppendLine($"- **Non-Analyzable:** {run.NonAnalyzable}");
        summary.AppendLine($"- **Detection Rate:** {detectionRate:F1}%");
        summary.AppendLine();
        summary.AppendLine("## Articles Generated");
        summary.AppendLine();
        summary.AppendLine($"- **Drafted:** {run.ArticlesDrafted}");
        summary.AppendLine($"- **Published:** {run.ArticlesPublished}");
        summary.AppendLine();

        return summary.ToString();
    }
}
