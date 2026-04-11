// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;

namespace GauntletCI.Core.Diff;

/// <summary>
/// Parses unified git diff text into a <see cref="DiffContext"/>.
/// Uses git.exe output — no custom diff implementation.
/// </summary>
public static class DiffParser
{
    private static readonly Regex HunkHeader =
        new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

    private static readonly Regex FileHeader =
        new(@"^diff --git a/(.+) b/(.+)$", RegexOptions.Compiled);

    private static readonly Regex AddedFileMarker =
        new(@"^new file mode", RegexOptions.Compiled);

    private static readonly Regex DeletedFileMarker =
        new(@"^deleted file mode", RegexOptions.Compiled);

    public static DiffContext Parse(string rawDiff, string commitSha = "", string? commitMessage = null)
    {
        var files = new List<DiffFile>();
        var lines = rawDiff.Split('\n');

        DiffFile? currentFile = null;
        DiffHunk? currentHunk = null;
        bool isAdded = false;
        bool isDeleted = false;
        int oldLine = 0;
        int newLine = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');

            var fileMatch = FileHeader.Match(line);
            if (fileMatch.Success)
            {
                if (currentFile != null)
                    files.Add(FinalizeFile(currentFile, isAdded, isDeleted));

                currentFile = new DiffFile
                {
                    OldPath = fileMatch.Groups[1].Value,
                    NewPath = fileMatch.Groups[2].Value
                };
                currentHunk = null;
                isAdded = false;
                isDeleted = false;
                continue;
            }

            // Bare unified diff format: "--- a/path" starts a new file when there is no
            // preceding "diff --git" header. Trigger when no file is open yet, or when
            // we are already past the @@ stage of a previous file (currentHunk != null).
            if (line.StartsWith("--- a/") && (currentFile == null || currentHunk != null))
            {
                if (currentFile != null)
                    files.Add(FinalizeFile(currentFile, isAdded, isDeleted));

                var path = line[6..];
                currentFile = new DiffFile { OldPath = path, NewPath = path };
                currentHunk = null;
                isAdded = false;
                isDeleted = false;
                continue;
            }

            if (currentFile == null) continue;

            // Update new path from "+++ b/path" (bare format or git format — safe to apply always)
            if (line.StartsWith("+++ b/")) { currentFile.NewPath = line[6..]; continue; }

            if (AddedFileMarker.IsMatch(line)) { isAdded = true; continue; }
            if (DeletedFileMarker.IsMatch(line)) { isDeleted = true; continue; }

            // Skip git diff header lines (index, old-path, new-path)
            if (line.StartsWith("index ") || line.StartsWith("--- ") || line.StartsWith("+++ "))
                continue;

            var hunkMatch = HunkHeader.Match(line);
            if (hunkMatch.Success)
            {
                currentHunk = new DiffHunk
                {
                    OldStartLine = int.Parse(hunkMatch.Groups[1].Value),
                    NewStartLine = int.Parse(hunkMatch.Groups[2].Value)
                };
                currentFile.Hunks.Add(currentHunk);
                oldLine = currentHunk.OldStartLine;
                newLine = currentHunk.NewStartLine;
                continue;
            }

            if (currentHunk == null) continue;

            // Skip git meta lines (e.g. "\ No newline at end of file")
            if (line.StartsWith('\\')) continue;

            if (line.StartsWith('+'))
            {
                currentHunk.Lines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Added,
                    LineNumber = newLine++,
                    OldLineNumber = 0,
                    Content = line[1..]
                });
            }
            else if (line.StartsWith('-'))
            {
                currentHunk.Lines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Removed,
                    LineNumber = 0,
                    OldLineNumber = oldLine++,
                    Content = line[1..]
                });
            }
            else if (line.StartsWith(' '))
            {
                currentHunk.Lines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Context,
                    LineNumber = newLine++,
                    OldLineNumber = oldLine++,
                    Content = line[1..]
                });
            }
        }

        if (currentFile != null)
            files.Add(FinalizeFile(currentFile, isAdded, isDeleted));

        return new DiffContext
        {
            RawDiff = rawDiff,
            CommitSha = commitSha,
            CommitMessage = commitMessage,
            Files = files
        };
    }

    /// <summary>
    /// Shells out to git to get the diff for a commit or range.
    /// </summary>
    public static async Task<DiffContext> FromGitAsync(
        string repoPath, string commitRef, CancellationToken ct = default)
    {
        var (diff, message) = await RunGitAsync(repoPath, commitRef, ct);
        return Parse(diff, commitRef, message);
    }

    /// <summary>Analyzes only staged changes (git diff --cached).</summary>
    public static async Task<DiffContext> FromStagedAsync(
        string repoPath, CancellationToken ct = default)
    {
        var diff = await RunProcessAsync("git", $"-C \"{repoPath}\" diff --cached", ct);
        return Parse(diff, commitSha: "staged");
    }

    /// <summary>Analyzes only unstaged changes (git diff).</summary>
    public static async Task<DiffContext> FromUnstagedAsync(
        string repoPath, CancellationToken ct = default)
    {
        var diff = await RunProcessAsync("git", $"-C \"{repoPath}\" diff", ct);
        return Parse(diff, commitSha: "unstaged");
    }

    /// <summary>Analyzes all local changes: staged + unstaged combined (git diff HEAD).</summary>
    public static async Task<DiffContext> FromAllChangesAsync(
        string repoPath, CancellationToken ct = default)
    {
        var diff = await RunProcessAsync("git", $"-C \"{repoPath}\" diff HEAD", ct);
        return Parse(diff, commitSha: "all-changes");
    }

    /// <summary>Parses a diff file from disk.</summary>
    public static DiffContext FromFile(string diffFilePath)
    {
        var raw = File.ReadAllText(diffFilePath);
        return Parse(raw);
    }

    private static DiffFile FinalizeFile(DiffFile f, bool isAdded, bool isDeleted) =>
        new()
        {
            OldPath = f.OldPath,
            NewPath = f.NewPath,
            IsAdded = isAdded,
            IsDeleted = isDeleted,
            Hunks = f.Hunks
        };

    private static async Task<(string diff, string? message)> RunGitAsync(
        string repoPath, string commitRef, CancellationToken ct)
    {
        // Get commit message
        string? message = null;
        try
        {
            var msgResult = await RunProcessAsync("git", $"-C \"{repoPath}\" log -1 --format=%s {commitRef}", ct);
            message = msgResult.Trim();
        }
        catch { /* non-fatal */ }

        // Get diff — for a single commit use commit^..commit; for a range pass as-is
        var diffArg = commitRef.Contains("..") ? commitRef : $"{commitRef}^..{commitRef}";
        var diff = await RunProcessAsync("git", $"-C \"{repoPath}\" diff {diffArg}", ct);
        return (diff, message);
    }

    private static async Task<string> RunProcessAsync(string executable, string arguments, CancellationToken ct)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        // Read stdout and stderr concurrently to prevent deadlocks on large output.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new GitProcessException($"{executable} {arguments}", process.ExitCode, stderr);

        return output;
    }
}
