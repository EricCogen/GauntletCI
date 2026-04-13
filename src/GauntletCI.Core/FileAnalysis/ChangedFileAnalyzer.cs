// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.FileAnalysis;

/// <summary>
/// Classifies each changed file and determines whether it is eligible for rule analysis.
/// Version 1 allowlist: only .cs files are eligible.
/// </summary>
public sealed class ChangedFileAnalyzer : IChangedFileAnalyzer
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs"
        };

    public ChangedFileAnalysisRecord Analyze(DiffFile file)
    {
        var filePath = file.NewPath ?? string.Empty;
        var oldPath = string.IsNullOrEmpty(file.OldPath) ? null : file.OldPath;
        var isDeleted = file.IsDeleted;
        var isRename = file.IsRenamed;
        var hasContentChanges = file.Hunks.Count > 0;

        if (string.IsNullOrEmpty(filePath))
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = string.Empty,
                Classification     = FileEligibilityClassification.EmptyPath,
                IsEligible         = false,
                Reason             = "File path was empty",
                IsDeleted          = isDeleted,
                IsRename           = isRename,
                HasContentChanges  = hasContentChanges,
            };
        }

        if (isDeleted)
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = Path.GetExtension(filePath),
                Classification     = FileEligibilityClassification.Deleted,
                IsEligible         = false,
                Reason             = "File was deleted and is skipped in v1",
                IsDeleted          = true,
                IsRename           = isRename,
                HasContentChanges  = hasContentChanges,
            };
        }

        if (isRename && !hasContentChanges)
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = Path.GetExtension(filePath),
                Classification     = FileEligibilityClassification.RenamedOnly,
                IsEligible         = false,
                Reason             = "File was renamed without content changes and is skipped",
                IsDeleted          = false,
                IsRename           = true,
                HasContentChanges  = false,
            };
        }

        if (IsLikelyGenerated(filePath))
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = Path.GetExtension(filePath),
                Classification     = FileEligibilityClassification.Generated,
                IsEligible         = false,
                Reason             = "File appears to be generated code and is skipped",
                IsDeleted          = false,
                IsRename           = isRename,
                HasContentChanges  = hasContentChanges,
                IsGenerated        = true,
            };
        }

        var extension = Path.GetExtension(filePath);

        if (string.IsNullOrEmpty(extension))
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = string.Empty,
                Classification     = FileEligibilityClassification.MissingExtension,
                IsEligible         = false,
                Reason             = "File had no extension",
                IsDeleted          = false,
                IsRename           = isRename,
                HasContentChanges  = hasContentChanges,
            };
        }

        if (AllowedExtensions.Contains(extension))
        {
            return new ChangedFileAnalysisRecord
            {
                FilePath           = filePath,
                OldFilePath        = oldPath,
                Extension          = extension,
                Classification     = FileEligibilityClassification.EligibleSource,
                IsEligible         = true,
                Reason             = $"Extension {extension} is allowed for analysis",
                IsDeleted          = false,
                IsRename           = isRename,
                HasContentChanges  = hasContentChanges,
            };
        }

        return new ChangedFileAnalysisRecord
        {
            FilePath           = filePath,
            OldFilePath        = oldPath,
            Extension          = extension,
            Classification     = FileEligibilityClassification.UnknownUnsupported,
            IsEligible         = false,
            Reason             = $"Extension {extension} is not supported for analysis",
            IsDeleted          = false,
            IsRename           = isRename,
            HasContentChanges  = hasContentChanges,
        };
    }

    private static bool IsLikelyGenerated(string filePath) =>
        filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || filePath.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
        || filePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
}
