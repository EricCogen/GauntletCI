namespace GauntletCI.Watchtower.Models;

public enum CVEStatus
{
    New,
    Analyzed,
    Detected,
    Missed,
    NonAnalyzable,
    Published
}

public enum RunStatus
{
    Running,
    Completed,
    Failed
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum AlertType
{
    BuildFailure,
    FeedError,
    HighSeverityMiss,
    DetectionMilestone,
    ValidationError
}

public enum VulnerabilityClass
{
    NullDereference,
    InputValidationBypass,
    AuthenticationBypass,
    AuthorizationBypass,
    CryptographicWeakness,
    InformationDisclosure,
    DenialOfService,
    RemoteCodeExecution,
    RaceCondition,
    Other
}
