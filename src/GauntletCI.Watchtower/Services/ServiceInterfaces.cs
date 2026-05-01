using GauntletCI.Watchtower.Models.Entities;

namespace GauntletCI.Watchtower.Services;

public interface ICVEFeedService
{
    Task<List<CVEFeedEntry>> PollNewCvesAsync();
}

public interface ITechnicalAnalysisService
{
    Task AnalyzeCveAsync(CVEFeedEntry feedEntry);
}

public interface IGauntletSyncService
{
    Task<string> SyncRepositoryAsync();
}

public interface IGauntletBuildService
{
    Task<bool> BuildGauntletCiAsync(GauntletRun gauntletRun);
}

public interface IValidationExecutor
{
    Task<(bool Success, string Stdout, string Stderr, int ExitCode)> ValidateAsync(string diffPath, string cliPath);
}

public interface IResultParser
{
    (bool Detected, float Confidence, List<string> TriggeredRules) ParseResults(string jsonOutput);
}

public interface IArticleGeneratorService
{
    Task<ArticleDraft> GenerateArticleDraftAsync(ValidationResult result, CVE cve);
}

public interface IGapAnalyzerService
{
    Task<Miss> AnalyzeMissAsync(ValidationResult result, CVE cve);
}

public interface IArticlePublisherService
{
    Task<bool> PublishArticleAsync(ArticleDraft draft);
}

public interface IAlertService
{
    Task CreateAlertAsync(Models.AlertType type, Models.AlertSeverity severity, string description);
}

public interface IRunSummaryService
{
    Task<string> GenerateSummaryAsync(ValidationRun run);
}
