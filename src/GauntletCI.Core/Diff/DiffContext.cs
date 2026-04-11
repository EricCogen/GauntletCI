// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Diff;

/// <summary>One changed hunk within a file.</summary>
public class DiffHunk
{
    public int OldStartLine { get; init; }
    public int NewStartLine { get; init; }
    public List<DiffLine> Lines { get; init; } = [];
}

public enum DiffLineKind { Added, Removed, Context }

public class DiffLine
{
    public DiffLineKind Kind { get; init; }
    public int LineNumber { get; init; }    // new-file line number (0 for removed)
    public int OldLineNumber { get; init; } // old-file line number (0 for added)
    public string Content { get; init; } = string.Empty;
}

/// <summary>Represents one changed file within a diff.</summary>
public class DiffFile
{
    public string OldPath { get; init; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public bool IsAdded { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsRenamed => OldPath != NewPath && !IsAdded && !IsDeleted;
    public List<DiffHunk> Hunks { get; init; } = [];

    public IEnumerable<DiffLine> AddedLines =>
        Hunks.SelectMany(h => h.Lines).Where(l => l.Kind == DiffLineKind.Added);

    public IEnumerable<DiffLine> RemovedLines =>
        Hunks.SelectMany(h => h.Lines).Where(l => l.Kind == DiffLineKind.Removed);
}

/// <summary>The full parsed diff — all changed files and their hunks.</summary>
public class DiffContext
{
    public string RawDiff { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public string? CommitMessage { get; init; }
    public List<DiffFile> Files { get; init; } = [];

    public IEnumerable<DiffLine> AllAddedLines =>
        Files.SelectMany(f => f.AddedLines);

    public IEnumerable<DiffLine> AllRemovedLines =>
        Files.SelectMany(f => f.RemovedLines);
}
