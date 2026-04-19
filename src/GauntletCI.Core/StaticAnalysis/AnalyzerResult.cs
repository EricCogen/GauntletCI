// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Language-neutral container for static analysis diagnostics.
/// JSON-serialisable so it can be cached or passed to cloud features later.
/// </summary>
public class AnalyzerResult
{
    public List<AnalyzerDiagnostic> Diagnostics { get; init; } = [];
    public string AnalyzedFile { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Primary target framework moniker detected from the repo's .csproj files
    /// (e.g. <c>net8.0</c>, <c>net6.0</c>). Null when detection was not possible.
    /// </summary>
    public string? TargetFramework { get; init; }
}

public class AnalyzerDiagnostic
{
    public required string Id { get; init; }        // e.g. CA2100
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string Severity { get; init; } = "Warning";
    public string Category { get; init; } = string.Empty;
}
