// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Diff;

/// <summary>One changed hunk within a file.</summary>
public class DiffHunk
{
    /// <summary>The first line number in the old file covered by this hunk.</summary>
    public int OldStartLine { get; init; }
    /// <summary>The first line number in the new file covered by this hunk.</summary>
    public int NewStartLine { get; init; }
    /// <summary>All lines (added, removed, and context) belonging to this hunk.</summary>
    public List<DiffLine> Lines { get; init; } = [];
}

/// <summary>Indicates whether a diff line was added, removed, or unchanged context.</summary>
public enum DiffLineKind { Added, Removed, Context }

/// <summary>A single line within a diff hunk, with kind and line-number metadata.</summary>
public class DiffLine
{
    /// <summary>Whether this line was added, removed, or kept as context.</summary>
    public DiffLineKind Kind { get; init; }
    /// <summary>The line number in the new file; 0 for removed lines.</summary>
    public int LineNumber { get; init; }    // new-file line number (0 for removed)
    /// <summary>The line number in the old file; 0 for added lines.</summary>
    public int OldLineNumber { get; init; } // old-file line number (0 for added)
    /// <summary>The raw text content of the line, without the leading +/- prefix.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>Represents one changed file within a diff.</summary>
public class DiffFile
{
    /// <summary>The file path before the change; empty for newly added files.</summary>
    public string OldPath { get; init; } = string.Empty;
    /// <summary>The file path after the change; used as the canonical path for analysis.</summary>
    public string NewPath { get; set; } = string.Empty;
    /// <summary>True when the file did not exist before this commit.</summary>
    public bool IsAdded { get; init; }
    /// <summary>True when the file was removed by this commit.</summary>
    public bool IsDeleted { get; init; }
    /// <summary>True when the file was moved or renamed without being added or deleted outright.</summary>
    public bool IsRenamed => OldPath != NewPath && !IsAdded && !IsDeleted;
    /// <summary>All changed hunks within this file.</summary>
    public List<DiffHunk> Hunks { get; init; } = [];

    /// <summary>All lines added in this file across every hunk.</summary>
    public IEnumerable<DiffLine> AddedLines =>
        Hunks.SelectMany(h => h.Lines).Where(l => l.Kind == DiffLineKind.Added);

    /// <summary>All lines removed from this file across every hunk.</summary>
    public IEnumerable<DiffLine> RemovedLines =>
        Hunks.SelectMany(h => h.Lines).Where(l => l.Kind == DiffLineKind.Removed);
}

/// <summary>The full parsed diff — all changed files and their hunks.</summary>
public class DiffContext
{
    /// <summary>The original unified diff text this context was parsed from.</summary>
    public string RawDiff { get; init; } = string.Empty;
    /// <summary>The Git commit SHA associated with this diff, or a sentinel such as "staged".</summary>
    public string CommitSha { get; init; } = string.Empty;
    /// <summary>The commit message subject line, if available.</summary>
    public string? CommitMessage { get; init; }
    /// <summary>All changed files parsed from the diff.</summary>
    public List<DiffFile> Files { get; init; } = [];

    /// <summary>All added lines across every file in this diff.</summary>
    public IEnumerable<DiffLine> AllAddedLines =>
        Files.SelectMany(f => f.AddedLines);

    /// <summary>All removed lines across every file in this diff.</summary>
    public IEnumerable<DiffLine> AllRemovedLines =>
        Files.SelectMany(f => f.RemovedLines);
}
