// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Analysis;

/// <summary>
/// Full-codebase analyzer: scans all files in a directory tree, converting them to a synthetic diff
/// where all lines are marked as "Added" so that rules can evaluate the entire codebase.
/// Useful for retroactive compliance checks and detecting pre-existing violations.
/// </summary>
public static class CodebaseAnalyzer
{
    private static readonly string[] CSharpExtensions = [".cs"];
    private static readonly string[] ExcludePatterns = ["bin", "obj", ".git", "node_modules", ".github"];

    /// <summary>
    /// Metadata about a codebase scan (used for user feedback before analysis).
    /// </summary>
    public class ScanMetadata
    {
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public int EstimatedLinesOfCode { get; set; }
        public double EstimatedSeconds { get; set; }
    }

    /// <summary>
    /// Gathers metadata about a codebase directory (file count, size, LOC estimate) for user feedback
    /// before performing the full scan. Fast operation (~100ms even for 1000+ files).
    /// </summary>
    /// <param name="rootPath">The root directory to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metadata suitable for displaying to user before scan</returns>
    public static async Task<ScanMetadata> GetScanMetadataAsync(string rootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

        var csFiles = ScanCSharpFiles(new DirectoryInfo(rootPath));
        long totalBytes = 0;
        int estimatedLoc = 0;

        // Sample file sizes/LOC (don't read entire files, just headers for speed)
        foreach (var file in csFiles.Take(Math.Min(100, csFiles.Count)))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file.FullName);
                totalBytes += info.Length;
                
                // Estimate LOC by reading first 1KB (or entire file if smaller)
                var header = await File.ReadAllTextAsync(file.FullName, ct);
                var locEstimate = header.Count(c => c == '\n') + 1;
                estimatedLoc += locEstimate;
            }
            catch { /* skip unreadable files */ }
        }

        // Scale up LOC estimate if sampling
        if (csFiles.Count > 100)
            estimatedLoc = (int)(estimatedLoc * ((double)csFiles.Count / 100));

        // Time estimate: ~0.001s per file on typical hardware
        var estimatedSeconds = Math.Max(1, csFiles.Count * 0.001);

        return new ScanMetadata
        {
            FileCount = csFiles.Count,
            TotalBytes = totalBytes,
            EstimatedLinesOfCode = estimatedLoc,
            EstimatedSeconds = estimatedSeconds,
        };
    }

    /// <summary>
    /// Converts an entire directory tree of C# files into a synthetic DiffContext.
    /// All lines are marked as "Added" so rules treat this as analyzing new code.
    /// </summary>
    /// <param name="rootPath">The root directory to scan (e.g., ./src)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A DiffContext representing all C# files in the directory</returns>
    public static async Task<DiffContext> ScanDirectoryAsync(string rootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

        var diffFiles = new List<DiffFile>();
        var di = new DirectoryInfo(rootPath);
        var csFiles = ScanCSharpFiles(di);

        foreach (var file in csFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file.FullName, ct);
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                // Create a synthetic "file added" diff where all lines are marked as Added
                var diffFile = new DiffFile
                {
                    NewPath = MakeRelativePath(rootPath, file.FullName),
                    IsAdded = true,
                    Hunks =
                    [
                        new DiffHunk
                        {
                            OldStartLine = 0,
                            NewStartLine = 1,
                            Lines = lines
                                .Select((line, idx) => new DiffLine
                                {
                                    Kind = DiffLineKind.Added,
                                    LineNumber = idx + 1,
                                    OldLineNumber = 0,
                                    Content = line,
                                })
                                .ToList(),
                        },
                    ],
                };

                diffFiles.Add(diffFile);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Failed to read file {file.FullName}: {ex.Message}");
            }
        }

        return new DiffContext
        {
            RawDiff = string.Empty,
            CommitSha = "codebase-scan",
            CommitMessage = $"Full codebase analysis of {rootPath}",
            Files = diffFiles,
        };
    }

    /// <summary>
    /// Recursively scans a directory for all C# source files, excluding build/cache directories.
    /// </summary>
    private static List<FileInfo> ScanCSharpFiles(DirectoryInfo rootDir)
    {
        var results = new List<FileInfo>();

        try
        {
            // Add C# files from current directory
            results.AddRange(
                rootDir.GetFiles("*.cs")
                    .Where(f => !ShouldExclude(f.FullName))
            );

            // Recursively scan subdirectories
            foreach (var subDir in rootDir.GetDirectories())
            {
                if (ShouldExclude(subDir.FullName))
                    continue;

                results.AddRange(ScanCSharpFiles(subDir));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't read
        }

        return results;
    }

    /// <summary>
    /// Checks if a path should be excluded from analysis.
    /// </summary>
    private static bool ShouldExclude(string path)
    {
        var pathLower = path.ToLowerInvariant();
        return ExcludePatterns.Any(pattern => pathLower.Contains($"{Path.DirectorySeparatorChar}{pattern}{Path.DirectorySeparatorChar}") ||
                                             pathLower.Contains($"{Path.DirectorySeparatorChar}{pattern}")); }

    /// <summary>
    /// Converts an absolute file path to a relative path from the root directory.
    /// </summary>
    private static string MakeRelativePath(string rootPath, string fullPath)
    {
        var root = new DirectoryInfo(rootPath).FullName.TrimEnd(Path.DirectorySeparatorChar);
        var file = new FileInfo(fullPath).FullName;

        if (file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = file[root.Length..].TrimStart(Path.DirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/'); // Normalize to forward slashes
        }

        return file;
    }
}
