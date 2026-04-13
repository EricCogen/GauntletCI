// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.FileAnalysis;

public sealed class ChangedFileAnalysisRecord
{
    public string FilePath { get; init; } = string.Empty;
    public string? OldFilePath { get; init; }
    public string Extension { get; init; } = string.Empty;
    public FileEligibilityClassification Classification { get; init; }
    public bool IsEligible { get; init; }
    public string Reason { get; init; } = string.Empty;

    public bool IsDeleted { get; init; }
    public bool IsRename { get; init; }
    public bool HasContentChanges { get; init; }

    public long? AddedLines { get; init; }
    public long? DeletedLines { get; init; }

    public bool? IsBinary { get; init; }
    public bool? IsGenerated { get; init; }
}
