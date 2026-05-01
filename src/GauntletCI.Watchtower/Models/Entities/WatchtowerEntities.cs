namespace GauntletCI.Watchtower.Models.Entities;

public class GauntletRun
{
    public int Id { get; set; }
    public string CommitSha { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public bool BuildSuccess { get; set; }
    public string BuildOutput { get; set; } = string.Empty;
    public DateTime BuildTimestamp { get; set; }
    public string CliArtifactPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CVEFeed
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int PollingIntervalHours { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CVEFeedEntry>? Entries { get; set; }
}

public class CVEFeedEntry
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public string CveId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float CvssScore { get; set; }
    public string SeverityRating { get; set; } = string.Empty;
    public string AdvisoryUrl { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public CVEStatus Status { get; set; } = CVEStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CVEFeed? Feed { get; set; }
    public CVE? CVE { get; set; }
}

public class CVE
{
    public int Id { get; set; }
    public string CveId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public float CvssScore { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime FirstSeenDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TechnicalData? TechnicalData { get; set; }
    public ICollection<ValidationResult>? ValidationResults { get; set; }
}

public class TechnicalData
{
    public int Id { get; set; }
    public int CveId { get; set; }
    public string LinkedRepositoryUrls { get; set; } = string.Empty;
    public string PatchUrls { get; set; } = string.Empty;
    public string AffectedVersions { get; set; } = string.Empty;
    public VulnerabilityClass VulnerabilityClass { get; set; }
    public string AnalysisNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CVE? CVE { get; set; }
}

public class ValidationRun
{
    public int Id { get; set; }
    public int GauntletRunId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Running;
    public int TotalAnalyzed { get; set; }
    public int Detected { get; set; }
    public int Missed { get; set; }
    public int NonAnalyzable { get; set; }
    public int ArticlesDrafted { get; set; }
    public int ArticlesPublished { get; set; }

    public GauntletRun? GauntletRun { get; set; }
    public ICollection<ValidationResult>? Results { get; set; }
}

public class ValidationResult
{
    public int Id { get; set; }
    public int ValidationRunId { get; set; }
    public int CveId { get; set; }
    public bool DetectionOccurred { get; set; }
    public float ConfidenceScore { get; set; }
    public string TriggeredRules { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string CapturedDiff { get; set; } = string.Empty;
    public string StdoutPath { get; set; } = string.Empty;
    public string StderrPath { get; set; } = string.Empty;
    public string AnalysisStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ValidationRun? ValidationRun { get; set; }
    public CVE? CVE { get; set; }
}

public class Article
{
    public int Id { get; set; }
    public string CveId { get; set; } = string.Empty;
    public int ValidationResultId { get; set; }
    public string GauntletVersionTested { get; set; } = string.Empty;
    public string ArticleBody { get; set; } = string.Empty;
    public string RulesFired { get; set; } = string.Empty;
    public float ConfidenceScores { get; set; }
    public DateTime PublicationDate { get; set; }
    public string ArticleUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ArticleDraft
{
    public int Id { get; set; }
    public string CveId { get; set; } = string.Empty;
    public int ValidationResultId { get; set; }
    public string DraftBody { get; set; } = string.Empty;
    public string ReviewNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class Miss
{
    public int Id { get; set; }
    public string CveId { get; set; } = string.Empty;
    public int ValidationResultId { get; set; }
    public string ReasonForMiss { get; set; } = string.Empty;
    public string SuggestedRuleEnhancements { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Alert
{
    public int Id { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
