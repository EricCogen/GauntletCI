namespace GauntletCI.Core.Models;

public sealed record EvaluationRequest(
    string WorkingDirectory,
    bool FullMode,
    bool FastMode,
    string? Rule,
    bool JsonOutput,
    bool NoTelemetry,
    string? ExplicitTestCommand = null,
    string? ProvidedDiff = null,
    string? LocalEndpoint = null);
