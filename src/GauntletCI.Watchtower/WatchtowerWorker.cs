using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GauntletCI.Watchtower.Data;
using GauntletCI.Watchtower.Models;
using GauntletCI.Watchtower.Models.Entities;
using GauntletCI.Watchtower.Services;

namespace GauntletCI.Watchtower;

public class WatchtowerWorker : BackgroundService
{
    private readonly WatchtowerDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WatchtowerWorker> _logger;
    private readonly ICVEFeedService _feedService;
    private readonly ITechnicalAnalysisService _analysisService;
    private readonly IGauntletSyncService _syncService;
    private readonly IGauntletBuildService _buildService;
    private readonly IValidationExecutor _executor;
    private readonly IResultParser _parser;
    private readonly IArticleGeneratorService _articleGenerator;
    private readonly IGapAnalyzerService _gapAnalyzer;
    private readonly IArticlePublisherService _articlePublisher;
    private readonly IAlertService _alertService;
    private readonly IRunSummaryService _summaryService;

    public WatchtowerWorker(
        WatchtowerDbContext dbContext,
        IConfiguration configuration,
        ILogger<WatchtowerWorker> logger,
        ICVEFeedService feedService,
        ITechnicalAnalysisService analysisService,
        IGauntletSyncService syncService,
        IGauntletBuildService buildService,
        IValidationExecutor executor,
        IResultParser parser,
        IArticleGeneratorService articleGenerator,
        IGapAnalyzerService gapAnalyzer,
        IArticlePublisherService articlePublisher,
        IAlertService alertService,
        IRunSummaryService summaryService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _feedService = feedService;
        _analysisService = analysisService;
        _syncService = syncService;
        _buildService = buildService;
        _executor = executor;
        _parser = parser;
        _articleGenerator = articleGenerator;
        _gapAnalyzer = gapAnalyzer;
        _articlePublisher = articlePublisher;
        _alertService = alertService;
        _summaryService = summaryService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Watchtower worker starting");
        
        // Apply migrations on startup
        try
        {
            _logger.LogInformation("Applying database migrations");
            await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            await _alertService.CreateAlertAsync(
                AlertType.ValidationError,
                AlertSeverity.Critical,
                "Database migration failed: " + ex.Message);
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Watchtower worker executing");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunValidationCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation cycle");
                await _alertService.CreateAlertAsync(
                    AlertType.ValidationError,
                    AlertSeverity.Error,
                    "Validation cycle error: " + ex.Message);
            }

            // Wait before next cycle (can be configured)
            var delayHours = _configuration.GetValue<int?>("Validation:PollingIntervalHours") ?? 24;
            await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);
        }
    }

    private async Task RunValidationCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting validation cycle");

        var startTime = DateTime.UtcNow;

        // Step 1: Sync GauntletCI
        _logger.LogInformation("Step 1: Syncing GauntletCI repository");
        var commitSha = await _syncService.SyncRepositoryAsync();
        
        // Step 2: Build GauntletCI
        _logger.LogInformation("Step 2: Building GauntletCI");
        var gauntletRun = new GauntletRun
        {
            CommitSha = commitSha,
            Branch = _configuration.GetValue<string>("GauntletCI:Branch") ?? "main",
            BuildTimestamp = DateTime.UtcNow
        };
        
        gauntletRun.BuildSuccess = await _buildService.BuildGauntletCiAsync(gauntletRun);
        
        if (!gauntletRun.BuildSuccess)
        {
            _logger.LogError("GauntletCI build failed");
            await _alertService.CreateAlertAsync(
                AlertType.BuildFailure,
                AlertSeverity.Critical,
                "Failed to build GauntletCI");
            return;
        }

        gauntletRun.CliArtifactPath = _configuration.GetValue<string>("GauntletCI:PublishPath") ?? "./gauntletci-build/cli";
        _dbContext.GauntletRuns.Add(gauntletRun);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Step 3: Poll feeds
        _logger.LogInformation("Step 3: Polling security feeds");
        var newCves = await _feedService.PollNewCvesAsync();
        _logger.LogInformation("Found {Count} new CVEs", newCves.Count);

        // Step 4: Create validation run
        var validationRun = new ValidationRun
        {
            GauntletRunId = gauntletRun.Id,
            StartTime = startTime,
            Status = RunStatus.Running,
            TotalAnalyzed = newCves.Count
        };
        _dbContext.ValidationRuns.Add(validationRun);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Step 5: Analyze each CVE
        _logger.LogInformation("Step 5: Analyzing and validating CVEs");
        foreach (var feedEntry in newCves)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await AnalyzeAndValidateCveAsync(feedEntry, validationRun, gauntletRun, cancellationToken);
        }

        // Step 6: Finalize run
        _logger.LogInformation("Step 6: Finalizing validation run");
        validationRun.EndTime = DateTime.UtcNow;
        validationRun.Status = RunStatus.Completed;
        
        _dbContext.ValidationRuns.Update(validationRun);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Step 7: Generate summary
        var summary = await _summaryService.GenerateSummaryAsync(validationRun);
        _logger.LogInformation("Validation cycle completed. Summary: {Summary}", summary);
    }

    private async Task AnalyzeAndValidateCveAsync(
        CVEFeedEntry feedEntry,
        ValidationRun validationRun,
        GauntletRun gauntletRun,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing CVE {CveId}", feedEntry.CveId);

        try
        {
            // Analyze CVE for technical details
            await _analysisService.AnalyzeCveAsync(feedEntry);

            // Get or create CVE entity
            var cve = await _dbContext.CVEs.FirstOrDefaultAsync(c => c.CveId == feedEntry.CveId, cancellationToken);
            if (cve == null)
            {
                cve = new CVE
                {
                    CveId = feedEntry.CveId,
                    Title = feedEntry.Title,
                    Severity = feedEntry.SeverityRating,
                    CvssScore = feedEntry.CvssScore,
                    SourceUrl = feedEntry.AdvisoryUrl,
                    FirstSeenDate = feedEntry.PublishDate
                };
                _dbContext.CVEs.Add(cve);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Create validation result
            var validationResult = new ValidationResult
            {
                ValidationRunId = validationRun.Id,
                CveId = cve.Id
            };

            // TODO: Execute validation and parse results
            // For now, mark as placeholder
            validationResult.AnalysisStatus = "Placeholder";

            _dbContext.ValidationResults.Add(validationResult);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Generate article or miss report
            if (validationResult.DetectionOccurred)
            {
                validationRun.Detected++;
                var draft = await _articleGenerator.GenerateArticleDraftAsync(validationResult, cve);
                validationRun.ArticlesDrafted++;
            }
            else
            {
                validationRun.Missed++;
                await _gapAnalyzer.AnalyzeMissAsync(validationResult, cve);
            }

            feedEntry.Status = validationResult.DetectionOccurred ? CVEStatus.Detected : CVEStatus.Missed;
            _dbContext.CVEFeedEntries.Update(feedEntry);
            _dbContext.ValidationRuns.Update(validationRun);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CVE {CveId}", feedEntry.CveId);
            feedEntry.Status = CVEStatus.NonAnalyzable;
            validationRun.NonAnalyzable++;
            _dbContext.CVEFeedEntries.Update(feedEntry);
            _dbContext.ValidationRuns.Update(validationRun);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
