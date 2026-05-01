using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GauntletCI.Watchtower.Data;
using GauntletCI.Watchtower.Models;
using GauntletCI.Watchtower.Models.Entities;

namespace GauntletCI.Watchtower.Services;

public class CVEFeedService : ICVEFeedService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<CVEFeedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IAlertService _alertService;
    private static DateTime _lastNvdRequest = DateTime.MinValue;

    public CVEFeedService(
        WatchtowerDbContext dbContext,
        ILogger<CVEFeedService> logger,
        IConfiguration configuration,
        IAlertService alertService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
        _alertService = alertService;
        _httpClient = new HttpClient();
    }

    public async Task<List<CVEFeedEntry>> PollNewCvesAsync()
    {
        _logger.LogInformation("Polling NVD and GHSA security feeds for new CVEs");

        var newEntries = new List<CVEFeedEntry>();

        try
        {
            // Poll NVD first
            var nvdEntries = await PollNvdAsync();
            newEntries.AddRange(nvdEntries);

            // Then poll GHSA
            var ghsaEntries = await PollGhsaAsync();
            newEntries.AddRange(ghsaEntries);

            _logger.LogInformation("Polling complete. Found {Count} new CVEs total", newEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling security feeds");
            await _alertService.CreateAlertAsync(AlertType.FeedError, AlertSeverity.Error, 
                $"Failed to poll security feeds: {ex.Message}");
        }

        return newEntries;
    }

    public async Task<List<CVEFeedEntry>> PollNvdAsync()
    {
        _logger.LogInformation("Starting NVD API polling");
        var nvdUrl = _configuration.GetValue<string>("NVD:ApiUrl") ?? "https://services.nvd.nist.gov/rest/json/cves/2.0";
        var timeoutSeconds = _configuration.GetValue<int>("NVD:TimeoutSeconds");
        var resultsPerPage = _configuration.GetValue<int>("NVD:ResultsPerPage");
        var maxPages = _configuration.GetValue<int>("NVD:MaxPages");

        var newEntries = new List<CVEFeedEntry>();
        var nvdFeed = _dbContext.CVEFeeds.FirstOrDefault(f => f.Type == "NVD");

        if (nvdFeed == null)
        {
            nvdFeed = new CVEFeed
            {
                Name = "NVD",
                Url = nvdUrl,
                Type = "NVD",
                Enabled = true,
                PollingIntervalHours = 24
            };
            _dbContext.CVEFeeds.Add(nvdFeed);
            await _dbContext.SaveChangesAsync();
        }

        int startIndex = 0;
        int pageCount = 0;
        int totalResultsPerPage = 0;

        do
        {
            // Rate limiting: NVD requires 6 seconds between requests
            var timeSinceLastRequest = DateTime.UtcNow - _lastNvdRequest;
            if (timeSinceLastRequest.TotalSeconds < 6)
            {
                var delayMs = (int)((6 - timeSinceLastRequest.TotalSeconds) * 1000);
                _logger.LogInformation("Rate limiting: waiting {DelayMs}ms before next NVD request", delayMs);
                await Task.Delay(delayMs);
            }

            _lastNvdRequest = DateTime.UtcNow;

            var requestUrl = $"{nvdUrl}?startIndex={startIndex}&resultsPerPage={resultsPerPage}";
            _logger.LogInformation("Fetching NVD page: {Url}", requestUrl);

            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                {
                    var response = await _httpClient.GetAsync(requestUrl, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("NVD API returned status {StatusCode}", response.StatusCode);
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            await _alertService.CreateAlertAsync(AlertType.FeedError, AlertSeverity.Warning,
                                "NVD API rate limit reached. Pausing requests.");
                            break;
                        }
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var entries = ParseNvdResponse(content, nvdFeed.Id);
                    newEntries.AddRange(entries);

                    // Check pagination
                    using (var doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("totalResults", out var totalResults))
                        {
                            var total = totalResults.GetInt32();
                            totalResultsPerPage = root.TryGetProperty("resultsPerPage", out var rpp) 
                                ? rpp.GetInt32() 
                                : resultsPerPage;

                            if (startIndex + totalResultsPerPage >= total)
                            {
                                _logger.LogInformation("Reached end of NVD results");
                                break;
                            }
                        }
                    }

                    startIndex += resultsPerPage;
                    pageCount++;

                    if (pageCount >= maxPages)
                    {
                        _logger.LogInformation("Reached max pages limit");
                        break;
                    }
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                _logger.LogWarning("NVD API request timeout at startIndex {StartIndex}", startIndex);
                await _alertService.CreateAlertAsync(AlertType.FeedError, AlertSeverity.Warning,
                    $"NVD API request timeout at page starting index {startIndex}");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NVD page at startIndex {StartIndex}", startIndex);
                await _alertService.CreateAlertAsync(AlertType.FeedError, AlertSeverity.Warning,
                    $"NVD API error: {ex.Message}");
                break;
            }

        } while (pageCount < maxPages);

        nvdFeed.LastPolledAt = DateTime.UtcNow;
        _dbContext.CVEFeeds.Update(nvdFeed);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("NVD polling complete. Added {Count} entries", newEntries.Count);
        return newEntries;
    }

    private List<CVEFeedEntry> ParseNvdResponse(string jsonContent, int feedId)
    {
        var entries = new List<CVEFeedEntry>();

        try
        {
            using (var doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;

                if (!root.TryGetProperty("vulnerabilities", out var vulnerabilities))
                {
                    return entries;
                }

                foreach (var vuln in vulnerabilities.EnumerateArray())
                {
                    try
                    {
                        if (!vuln.TryGetProperty("cve", out var cveObj))
                            continue;

                        // Extract CVE ID
                        if (!cveObj.TryGetProperty("id", out var cveIdElem))
                            continue;

                        var cveId = cveIdElem.GetString() ?? "";
                        if (string.IsNullOrEmpty(cveId))
                            continue;

                        // Check for duplicates
                        if (_dbContext.CVEFeedEntries.Any(e => e.FeedId == feedId && e.CveId == cveId))
                        {
                            _logger.LogDebug("Skipping duplicate CVE {CveId}", cveId);
                            continue;
                        }

                        // Extract description
                        var description = "";
                        if (cveObj.TryGetProperty("descriptions", out var descriptions))
                        {
                            foreach (var desc in descriptions.EnumerateArray())
                            {
                                if (desc.TryGetProperty("value", out var descValue))
                                {
                                    description = descValue.GetString() ?? "";
                                    break;
                                }
                            }
                        }

                        // Extract CVSS score and severity
                        float cvssScore = 0f;
                        string severity = "UNKNOWN";
                        if (cveObj.TryGetProperty("metrics", out var metrics))
                        {
                            if (metrics.TryGetProperty("cvssV3_1", out var cvssV31))
                            {
                                if (cvssV31.TryGetProperty("baseScore", out var score))
                                {
                                    cvssScore = score.GetSingle();
                                }
                                if (cvssV31.TryGetProperty("baseSeverity", out var sev))
                                {
                                    severity = sev.GetString() ?? "UNKNOWN";
                                }
                            }
                            else if (metrics.TryGetProperty("cvssV3_0", out var cvssV30))
                            {
                                if (cvssV30.TryGetProperty("baseScore", out var score))
                                {
                                    cvssScore = score.GetSingle();
                                }
                                if (cvssV30.TryGetProperty("baseSeverity", out var sev))
                                {
                                    severity = sev.GetString() ?? "UNKNOWN";
                                }
                            }
                        }

                        // Extract published date
                        var publishDate = DateTime.UtcNow;
                        if (cveObj.TryGetProperty("published", out var publishedElem))
                        {
                            if (DateTime.TryParse(publishedElem.GetString(), out var parsed))
                            {
                                publishDate = parsed;
                            }
                        }

                        // Create feed entry
                        var entry = new CVEFeedEntry
                        {
                            FeedId = feedId,
                            CveId = cveId,
                            Title = $"NVD {cveId}",
                            Description = description,
                            CvssScore = cvssScore,
                            SeverityRating = severity,
                            AdvisoryUrl = $"https://nvd.nist.gov/vuln/detail/{cveId}",
                            PublishDate = publishDate,
                            Status = Models.CVEStatus.New,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        entries.Add(entry);
                        _logger.LogDebug("Parsed NVD CVE {CveId} - CVSS: {CvssScore}, Severity: {Severity}", 
                            cveId, cvssScore, severity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing individual NVD vulnerability entry");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing NVD response");
        }

        return entries;
    }

    public async Task<List<CVEFeedEntry>> PollGhsaAsync()
    {
        _logger.LogInformation("Starting GHSA polling (placeholder - GitHub GraphQL would go here)");
        // Note: Full GHSA implementation requires GitHub token which isn't configured
        // This placeholder allows the system to function without authentication
        return new List<CVEFeedEntry>();
    }
}

public class TechnicalAnalysisService : ITechnicalAnalysisService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<TechnicalAnalysisService> _logger;
    private readonly IAlertService _alertService;

    // Attack pattern keywords for classification
    private static readonly Dictionary<string, string> AttackPatterns = new()
    {
        { "rce", "RemoteCodeExecution" },
        { "remote code execution", "RemoteCodeExecution" },
        { "execute arbitrary code", "RemoteCodeExecution" },
        { "command injection", "RemoteCodeExecution" },
        { "os command", "RemoteCodeExecution" },
        
        { "privilege escalation", "PrivilegeEscalation" },
        { "privesc", "PrivilegeEscalation" },
        { "elevate", "PrivilegeEscalation" },
        { "escalate privileges", "PrivilegeEscalation" },
        
        { "denial of service", "DoS" },
        { "dos", "DoS" },
        { "resource exhaustion", "DoS" },
        { "crash", "DoS" },
        { "hang", "DoS" },
        
        { "information disclosure", "InfoDisc" },
        { "leak", "InfoDisc" },
        { "sensitive data", "InfoDisc" },
        { "expose", "InfoDisc" },
        { "memory leak", "InfoDisc" },
        
        { "ssrf", "SSRF" },
        { "server-side request forgery", "SSRF" },
        { "internal request", "SSRF" },
        
        { "xss", "XSS" },
        { "cross-site scripting", "XSS" },
        { "script injection", "XSS" },
        { "dom-based", "XSS" }
    };

    public TechnicalAnalysisService(
        WatchtowerDbContext dbContext,
        ILogger<TechnicalAnalysisService> logger,
        IAlertService alertService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _alertService = alertService;
    }

    public async Task AnalyzeCveAsync(CVEFeedEntry feedEntry)
    {
        _logger.LogInformation("Analyzing CVE {CveId}", feedEntry.CveId);

        try
        {
            // Check if CVE already exists
            var existingCve = _dbContext.CVEs.FirstOrDefault(c => c.CveId == feedEntry.CveId);
            if (existingCve != null)
            {
                _logger.LogDebug("CVE {CveId} already in database", feedEntry.CveId);
                return;
            }

            // Create CVE record
            var cve = new Models.Entities.CVE
            {
                CveId = feedEntry.CveId,
                Title = feedEntry.Title,
                Severity = feedEntry.SeverityRating,
                CvssScore = feedEntry.CvssScore,
                SourceUrl = feedEntry.AdvisoryUrl,
                FirstSeenDate = feedEntry.PublishDate,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.CVEs.Add(cve);
            await _dbContext.SaveChangesAsync();

            // Analyze description for attack patterns
            var technicalData = AnalyzeTechnicalDetails(cve.Id, feedEntry.Description);
            _dbContext.TechnicalData.Add(technicalData);
            await _dbContext.SaveChangesAsync();

            // Alert on critical CVEs
            if (feedEntry.CvssScore >= 9.0)
            {
                await _alertService.CreateAlertAsync(
                    AlertType.FeedError,
                    AlertSeverity.Critical,
                    $"CRITICAL CVE FOUND: {feedEntry.CveId} with CVSS {feedEntry.CvssScore}");
            }

            _logger.LogInformation("CVE {CveId} analysis complete", feedEntry.CveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing CVE {CveId}", feedEntry.CveId);
            await _alertService.CreateAlertAsync(
                AlertType.ValidationError,
                AlertSeverity.Warning,
                $"Failed to analyze CVE {feedEntry.CveId}: {ex.Message}");
        }
    }

    public async Task<TechnicalAnalysis> AnalyzeAsync(List<CVEFeedEntry> entries)
    {
        _logger.LogInformation("Analyzing batch of {Count} CVE entries", entries.Count);
        
        var analysis = new TechnicalAnalysis { AnalyzedAt = DateTime.UtcNow };
        
        try
        {
            foreach (var entry in entries)
            {
                await AnalyzeCveAsync(entry);
                analysis.CveId = entry.CveId; // Track last processed
            }

            _logger.LogInformation("Batch analysis complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch analysis");
        }

        return analysis;
    }

    private Models.Entities.TechnicalData AnalyzeTechnicalDetails(int cveId, string description)
    {
        var analysisNotes = new List<string>();
        var vulnerabilityClass = Models.VulnerabilityClass.Other;

        var lowerDesc = description.ToLower();

        // Detect attack patterns
        var detectedPatterns = new List<string>();
        foreach (var pattern in AttackPatterns)
        {
            if (lowerDesc.Contains(pattern.Key))
            {
                detectedPatterns.Add(pattern.Value);
            }
        }

        if (detectedPatterns.Count > 0)
        {
            analysisNotes.Add($"Detected attack patterns: {string.Join(", ", detectedPatterns.Distinct())}");
            
            // Classify as most severe detected type
            vulnerabilityClass = detectedPatterns.First() switch
            {
                "RemoteCodeExecution" => Models.VulnerabilityClass.RemoteCodeExecution,
                "PrivilegeEscalation" => Models.VulnerabilityClass.AuthenticationBypass,
                "DoS" => Models.VulnerabilityClass.DenialOfService,
                "InfoDisc" => Models.VulnerabilityClass.InformationDisclosure,
                "SSRF" => Models.VulnerabilityClass.InputValidationBypass,
                "XSS" => Models.VulnerabilityClass.InputValidationBypass,
                _ => Models.VulnerabilityClass.Other
            };
        }

        // Extract potential affected versions (keywords like "version", ">=", "<=")
        var versionsMatch = System.Text.RegularExpressions.Regex.Matches(
            lowerDesc, 
            @"(?:version|v)\.?\s*(?:[0-9]+\.)+[0-9]+|(?:<=?|>=?)\s*[0-9]+\.(?:[0-9]+\.)*[0-9]+");
        
        if (versionsMatch.Count > 0)
        {
            analysisNotes.Add($"Potential affected versions: {string.Join(", ", versionsMatch.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct())}");
        }

        // Generate test diff recommendation
        analysisNotes.Add("Recommended test: Create diff targeting the vulnerability pattern identified in advisory");

        return new Models.Entities.TechnicalData
        {
            CveId = cveId,
            VulnerabilityClass = vulnerabilityClass,
            AnalysisNotes = string.Join("; ", analysisNotes),
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class GauntletSyncService : IGauntletSyncService
{
    private readonly ILogger<GauntletSyncService> _logger;

    public GauntletSyncService(ILogger<GauntletSyncService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SyncRepositoryAsync()
    {
        _logger.LogInformation("Syncing GauntletCI repository");
        
        // Implemented in GauntletServices.cs as GauntletSyncServiceImpl
        return "placeholder-commit-sha";
    }
}

public class GauntletBuildService : IGauntletBuildService
{
    private readonly ILogger<GauntletBuildService> _logger;

    public GauntletBuildService(ILogger<GauntletBuildService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> BuildGauntletCiAsync(GauntletRun gauntletRun)
    {
        _logger.LogInformation("Building GauntletCI from commit {CommitSha}", gauntletRun.CommitSha);
        
        // Implemented in GauntletServices.cs as GauntletBuildServiceImpl
        return true;
    }
}

public class ValidationExecutor : IValidationExecutor
{
    private readonly ILogger<ValidationExecutor> _logger;

    public ValidationExecutor(ILogger<ValidationExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string Stdout, string Stderr, int ExitCode)> ValidateAsync(string diffPath, string cliPath)
    {
        _logger.LogInformation("Executing validation with CLI {CliPath}", cliPath);
        
        // Implemented in ArticleAndAnalysisServices.cs as ValidationExecutorImpl
        return (true, string.Empty, string.Empty, 0);
    }
}

public class ArticleGeneratorService : IArticleGeneratorService
{
    private readonly ILogger<ArticleGeneratorService> _logger;

    public ArticleGeneratorService(ILogger<ArticleGeneratorService> logger)
    {
        _logger = logger;
    }

    public async Task<ArticleDraft> GenerateArticleDraftAsync(ValidationResult result, CVE cve)
    {
        _logger.LogInformation("Generating article draft for CVE {CveId}", cve.CveId);
        
        // Implemented in ArticleAndAnalysisServices.cs as ArticleGeneratorServiceImpl
        return new ArticleDraft
        {
            CveId = cve.CveId,
            ValidationResultId = result.Id,
            DraftBody = string.Empty
        };
    }
}

public class GapAnalyzerService : IGapAnalyzerService
{
    private readonly ILogger<GapAnalyzerService> _logger;

    public GapAnalyzerService(ILogger<GapAnalyzerService> logger)
    {
        _logger = logger;
    }

    public async Task<Miss> AnalyzeMissAsync(ValidationResult result, CVE cve)
    {
        _logger.LogInformation("Analyzing miss for CVE {CveId}", cve.CveId);
        
        // Implemented in ArticleAndAnalysisServices.cs as GapAnalyzerServiceImpl
        return new Miss
        {
            CveId = cve.CveId,
            ValidationResultId = result.Id,
            ReasonForMiss = string.Empty
        };
    }
}

public class ArticlePublisherService : IArticlePublisherService
{
    private readonly ILogger<ArticlePublisherService> _logger;

    public ArticlePublisherService(ILogger<ArticlePublisherService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> PublishArticleAsync(ArticleDraft draft)
    {
        _logger.LogInformation("Publishing article for CVE {CveId}", draft.CveId);
        
        // Implemented in ArticleAndAnalysisServices.cs as ArticlePublisherServiceImpl
        return true;
    }
}

public class AlertService : IAlertService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly ILogger<AlertService> _logger;

    public AlertService(WatchtowerDbContext dbContext, ILogger<AlertService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task CreateAlertAsync(Models.AlertType type, Models.AlertSeverity severity, string description)
    {
        _logger.LogInformation("Creating alert: {Type} - {Description}", type, severity);
        
        var alert = new Alert
        {
            Type = type,
            Severity = severity,
            Description = description
        };
        
        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync();
    }
}

public class RunSummaryService : IRunSummaryService
{
    private readonly ILogger<RunSummaryService> _logger;

    public RunSummaryService(ILogger<RunSummaryService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateSummaryAsync(ValidationRun run)
    {
        _logger.LogInformation("Generating summary for validation run {RunId}", run.Id);
        
        // Implemented in ArticleAndAnalysisServices.cs as RunSummaryServiceImpl
        return string.Empty;
    }
}
