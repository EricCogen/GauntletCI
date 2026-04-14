// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Runs Roslyn static analysis over the C# files changed in a diff.
/// Reads changed files from disk using the repo path as root.
/// Returns an aggregated <see cref="AnalyzerResult"/> with diagnostics from all analyzed files.
/// If no repo path is provided or no C# files are changed, returns a null result.
/// </summary>
public static class StaticAnalysisRunner
{
    private static readonly RoslynAnalyzer Analyzer = new();

    /// <summary>
    /// Analyzes all changed C# source files in the diff. Returns null when analysis
    /// cannot be performed (no repo path, no C# files, or diff-file-only mode).
    /// </summary>
    /// <param name="diff">The parsed diff whose changed C# files will be analyzed.</param>
    /// <param name="repoPath">Absolute path to the repository root used to locate source files on disk.</param>
    /// <param name="ct">Token used to cancel the analysis run.</param>
    /// <returns>
    /// An aggregated <see cref="AnalyzerResult"/> containing diagnostics from all analyzed files,
    /// or <c>null</c> if analysis could not be performed.
    /// </returns>
    public static async Task<AnalyzerResult?> RunAsync(
        DiffContext diff,
        string? repoPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(repoPath))
            return null;

        var csFiles = diff.Files
            .Where(f => !f.IsDeleted &&
                        f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (csFiles.Count == 0)
            return null;

        var allDiagnostics = new List<AnalyzerDiagnostic>();
        var anySuccess = false;

        foreach (var file in csFiles)
        {
            ct.ThrowIfCancellationRequested();

            // Normalize to OS path separator
            var relativePath = file.NewPath.Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(repoPath, relativePath);

            if (!File.Exists(absolutePath))
                continue;

            string sourceCode;
            try
            {
                sourceCode = await File.ReadAllTextAsync(absolutePath, ct);
            }
            catch (IOException)
            {
                continue;
            }

            var changedLines = file.AddedLines.Select(l => l.LineNumber).ToList();
            var result = await Analyzer.AnalyzeFileAsync(
                absolutePath, sourceCode, changedLines.Count > 0 ? changedLines : null, ct);

            if (result.Success)
            {
                anySuccess = true;
                allDiagnostics.AddRange(result.Diagnostics);
            }
        }

        if (!anySuccess && allDiagnostics.Count == 0)
            return null;

        return new AnalyzerResult
        {
            AnalyzedFile = $"[{csFiles.Count} file(s)]",
            Success = anySuccess,
            Diagnostics = allDiagnostics
        };
    }
}
