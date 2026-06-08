// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Reads C# source content aligned with the diff snapshot (index, commit, or worktree).
/// </summary>
internal static class GitSourceReader
{
    /// <summary>
    /// Returns file content matching the diff source, or null when unavailable.
    /// </summary>
    public static async Task<string?> TryReadAsync(
        string repoPath,
        DiffContext diff,
        string relativePath,
        CancellationToken ct = default)
    {
        if (!RepoPathResolver.TryResolvePathUnderRoot(repoPath, relativePath, out var absolutePath))
            return null;

        var source = diff.CommitSha;

        if (string.Equals(source, "unstaged", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "all-changes", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(source))
        {
            return await TryReadDiskAsync(absolutePath, ct).ConfigureAwait(false);
        }

        if (string.Equals(source, "staged", StringComparison.OrdinalIgnoreCase))
            return await TryReadGitBlobAsync(repoPath, $":{NormalizeGitPath(relativePath)}", ct).ConfigureAwait(false);

        var gitRef = ResolvePostImageRef(source);
        if (gitRef is null)
            return await TryReadDiskAsync(absolutePath, ct).ConfigureAwait(false);

        return await TryReadGitBlobAsync(repoPath, $"{gitRef}:{NormalizeGitPath(relativePath)}", ct).ConfigureAwait(false);
    }

    private static string? ResolvePostImageRef(string commitSha)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            return null;

        if (commitSha.Contains("...", StringComparison.Ordinal))
        {
            var parts = commitSha.Split("...", 2, StringSplitOptions.None);
            return parts.Length == 2 && parts[1].Length > 0 ? parts[1] : null;
        }

        if (commitSha.Contains("..", StringComparison.Ordinal))
        {
            var parts = commitSha.Split("..", 2, StringSplitOptions.None);
            return parts.Length == 2 && parts[1].Length > 0 ? parts[1] : null;
        }

        return commitSha;
    }

    private static string NormalizeGitPath(string path) =>
        path.Replace('\\', '/');

    private static async Task<string?> TryReadDiskAsync(string absolutePath, CancellationToken ct)
    {
        if (!File.Exists(absolutePath))
            return null;

        try
        {
            return await File.ReadAllTextAsync(absolutePath, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<string?> TryReadGitBlobAsync(string repoPath, string objectSpec, CancellationToken ct)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("-C");
            process.StartInfo.ArgumentList.Add(repoPath);
            process.StartInfo.ArgumentList.Add("show");
            process.StartInfo.ArgumentList.Add(objectSpec);

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
