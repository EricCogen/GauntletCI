// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Semantics;

/// <summary>
/// Converts from the existing DiffContext model to the new PatchModel semantic representation.
/// This adapter is the sole integration point between the existing diff layer and semantic layer.
/// All conversions are read-only; no modifications to DiffContext or existing behavior occur.
/// </summary>
public static class DiffToPatchAdapter
{
    /// <summary>
    /// Converts a DiffContext (from DiffParser) into a PatchModel (semantic patch representation).
    /// This is a pure data transformation with no side effects.
    /// </summary>
    /// <param name="diffContext">The existing diff context to convert.</param>
    /// <param name="source">Optional source identifier (defaults to "git diff").</param>
    /// <returns>A new PatchModel with equivalent structure and metadata.</returns>
    public static PatchModel FromDiffContext(Diff.DiffContext diffContext, string source = "git diff")
    {
        ArgumentNullException.ThrowIfNull(diffContext, nameof(diffContext));

        var patchFiles = diffContext.Files
            .Select(ConvertFile)
            .ToList();

        return new PatchModel
        {
            Files = patchFiles,
            Source = source,
            CommitSha = string.IsNullOrWhiteSpace(diffContext.CommitSha) ? null : diffContext.CommitSha,
            RawText = diffContext.RawDiff
        };
    }

    /// <summary>
    /// Converts a single DiffFile to a PatchFile, including file change kind detection.
    /// </summary>
    private static PatchFile ConvertFile(Diff.DiffFile diffFile)
    {
        var changeKind = DeterminePatchFileChangeKind(diffFile);
        var fileExtension = ExtractFileExtension(diffFile.NewPath);
        var (isTestFile, isProductionFile) = ClassifyFile(diffFile.NewPath, fileExtension);

        var hunks = diffFile.Hunks
            .Select(ConvertHunk)
            .ToList();

        return new PatchFile
        {
            OldPath = string.IsNullOrEmpty(diffFile.OldPath) ? null : diffFile.OldPath,
            NewPath = string.IsNullOrEmpty(diffFile.NewPath) ? null : diffFile.NewPath,
            ChangeKind = changeKind,
            Hunks = hunks,
            OldBlobSha = null, // DiffContext doesn't carry SHA info; could be added later
            NewBlobSha = null,
            FileExtension = fileExtension,
            IsTestFile = isTestFile,
            IsProductionFile = isProductionFile
        };
    }

    /// <summary>
    /// Determines the semantic change kind for a file.
    /// </summary>
    private static PatchFileChangeKind DeterminePatchFileChangeKind(Diff.DiffFile diffFile)
    {
        if (diffFile.IsAdded)
            return PatchFileChangeKind.Added;

        if (diffFile.IsDeleted)
            return PatchFileChangeKind.Deleted;

        if (diffFile.IsRenamed)
            return PatchFileChangeKind.Renamed;

        return PatchFileChangeKind.Modified;
    }

    /// <summary>
    /// Extracts the file extension from a file path.
    /// Returns null for files with no extension.
    /// </summary>
    private static string? ExtractFileExtension(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var lastDot = filePath.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == filePath.Length - 1)
            return null;

        return filePath[lastDot..].ToLowerInvariant();
    }

    /// <summary>
    /// Classifies a file as test or production based on heuristics.
    /// Returns (isTestFile, isProductionFile).
    /// </summary>
    private static (bool isTestFile, bool isProductionFile) ClassifyFile(string? filePath, string? extension)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return (false, false);

        var lowerPath = filePath.ToLowerInvariant();

        // Test file heuristics
        var isTestFile = lowerPath.Contains("test") ||
                         lowerPath.Contains("tests") ||
                         lowerPath.Contains("spec") ||
                         filePath!.Contains("Tests", StringComparison.OrdinalIgnoreCase);

        // Production file heuristics (check if it's likely code, not a data or config file)
        var isProductionFile = !isTestFile &&
                               (extension == ".cs" ||
                                extension == ".ts" ||
                                extension == ".tsx" ||
                                extension == ".js" ||
                                extension == ".jsx" ||
                                extension == ".py" ||
                                extension == ".java" ||
                                extension == ".go" ||
                                extension == ".rb" ||
                                extension == ".php" ||
                                extension == ".rs");

        return (isTestFile, isProductionFile);
    }

    /// <summary>
    /// Converts a single DiffHunk to a PatchHunk.
    /// </summary>
    private static PatchHunk ConvertHunk(Diff.DiffHunk diffHunk)
    {
        var lines = diffHunk.Lines
            .Select(ConvertLine)
            .ToList();

        return new PatchHunk
        {
            Header = $"@@ -{diffHunk.OldStartLine} +{diffHunk.NewStartLine} @@",
            OldStartLine = diffHunk.OldStartLine,
            OldLineCount = null, // DiffHunk doesn't track line counts directly
            NewStartLine = diffHunk.NewStartLine,
            NewLineCount = null,
            EnclosingSymbolHint = null, // Could be extracted from content analysis later
            Lines = lines
        };
    }

    /// <summary>
    /// Converts a single DiffLine to a PatchLine, translating line kinds.
    /// </summary>
    private static PatchLine ConvertLine(Diff.DiffLine diffLine)
    {
        var patchLineKind = diffLine.Kind switch
        {
            Diff.DiffLineKind.Added => PatchLineKind.Added,
            Diff.DiffLineKind.Removed => PatchLineKind.Removed,
            Diff.DiffLineKind.Context => PatchLineKind.Context,
            _ => PatchLineKind.Metadata
        };

        return new PatchLine
        {
            Kind = patchLineKind,
            Text = diffLine.Content,
            OldLineNumber = diffLine.OldLineNumber > 0 ? diffLine.OldLineNumber : null,
            NewLineNumber = diffLine.LineNumber > 0 ? diffLine.LineNumber : null
        };
    }
}
