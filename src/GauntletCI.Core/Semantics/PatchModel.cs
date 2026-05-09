// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Semantics;

/// <summary>
/// Represents the kind of change a file underwent in a patch.
/// Additive hierarchy: Added/Deleted → Renamed → ModeChanged → Modified/Unknown.
/// </summary>
public enum PatchFileChangeKind
{
    /// <summary>File content was modified.</summary>
    Modified,
    
    /// <summary>File was newly added.</summary>
    Added,
    
    /// <summary>File was removed.</summary>
    Deleted,
    
    /// <summary>File was renamed or moved.</summary>
    Renamed,
    
    /// <summary>File was copied (not all diff formats support this).</summary>
    Copied,
    
    /// <summary>File permissions or mode changed only.</summary>
    ModeChanged,
    
    /// <summary>Unknown or unclassified change.</summary>
    Unknown
}

/// <summary>
/// Classifies lines in a patch hunk by their semantic role.
/// Extends DiffLineKind with metadata markers.
/// </summary>
public enum PatchLineKind
{
    /// <summary>Line that was present in both old and new versions (context).</summary>
    Context,
    
    /// <summary>Line that was added in the patch.</summary>
    Added,
    
    /// <summary>Line that was removed in the patch.</summary>
    Removed,
    
    /// <summary>Special marker: "\ No newline at end of file".</summary>
    NoNewlineMarker,
    
    /// <summary>Metadata line (e.g., hunk header, file marker).</summary>
    Metadata
}

/// <summary>
/// Represents a single line within a patch hunk, with semantic and positional metadata.
/// </summary>
public sealed class PatchLine
{
    /// <summary>The semantic kind of this line (Added, Removed, Context, etc.).</summary>
    public PatchLineKind Kind { get; init; }
    
    /// <summary>The text content of the line, without leading +/- markers.</summary>
    public string Text { get; init; } = string.Empty;
    
    /// <summary>The line number in the old file; null for added lines.</summary>
    public int? OldLineNumber { get; init; }
    
    /// <summary>The line number in the new file; null for removed lines.</summary>
    public int? NewLineNumber { get; init; }
}

/// <summary>
/// Represents one hunk of changes within a patch file.
/// A hunk is a contiguous region of added, removed, and context lines.
/// </summary>
public sealed class PatchHunk
{
    /// <summary>The hunk header text (e.g., "@@ -10,5 +10,7 @@").</summary>
    public string Header { get; init; } = string.Empty;
    
    /// <summary>The starting line number in the old file.</summary>
    public int? OldStartLine { get; init; }
    
    /// <summary>The line count in the old file portion of this hunk.</summary>
    public int? OldLineCount { get; init; }
    
    /// <summary>The starting line number in the new file.</summary>
    public int? NewStartLine { get; init; }
    
    /// <summary>The line count in the new file portion of this hunk.</summary>
    public int? NewLineCount { get; init; }
    
    /// <summary>
    /// Optional hint about the enclosing symbol (e.g., method name, class name).
    /// Extracted from hunk header context when available.
    /// </summary>
    public string? EnclosingSymbolHint { get; init; }
    
    /// <summary>All lines (added, removed, context) in this hunk.</summary>
    public IReadOnlyList<PatchLine> Lines { get; init; } = [];
}

/// <summary>
/// Represents one changed file within a patch.
/// A patch file contains one or more hunks of changes.
/// </summary>
public sealed class PatchFile
{
    /// <summary>The file path before the change; empty for newly added files.</summary>
    public string? OldPath { get; init; }
    
    /// <summary>The file path after the change; used as the canonical identifier.</summary>
    public string? NewPath { get; init; }
    
    /// <summary>The semantic kind of change this file underwent.</summary>
    public PatchFileChangeKind ChangeKind { get; init; }
    
    /// <summary>All hunks of changes within this file.</summary>
    public IReadOnlyList<PatchHunk> Hunks { get; init; } = [];
    
    /// <summary>The old blob SHA (git object hash) if available.</summary>
    public string? OldBlobSha { get; init; }
    
    /// <summary>The new blob SHA (git object hash) if available.</summary>
    public string? NewBlobSha { get; init; }
    
    /// <summary>The file extension (e.g., ".cs", ".js"), computed from NewPath.</summary>
    public string? FileExtension { get; init; }
    
    /// <summary>True if this file is detected as a test file (heuristic-based).</summary>
    public bool IsTestFile { get; init; }
    
    /// <summary>True if this file is detected as production code (heuristic-based).</summary>
    public bool IsProductionFile { get; init; }
}

/// <summary>
/// Represents the full parsed patch: all changed files and their hunks.
/// Patch is the semantic model derived from a raw diff.
/// </summary>
public sealed class PatchModel
{
    /// <summary>All changed files in this patch.</summary>
    public IReadOnlyList<PatchFile> Files { get; init; } = [];
    
    /// <summary>
    /// The source of this patch (e.g., "git diff", "github pr", "staged", "commit").
    /// Useful for diagnostics and audit trails.
    /// </summary>
    public string Source { get; init; } = "unknown";
    
    /// <summary>The commit SHA or sentinel string (e.g., "staged") if available.</summary>
    public string? CommitSha { get; init; }
    
    /// <summary>The original raw diff text this patch was derived from.</summary>
    public string? RawText { get; init; }
    
    /// <summary>
    /// Counts the total number of added lines across all files and hunks.
    /// </summary>
    public int CountAddedLines()
    {
        return Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count(l => l.Kind == PatchLineKind.Added)));
    }
    
    /// <summary>
    /// Counts the total number of removed lines across all files and hunks.
    /// </summary>
    public int CountRemovedLines()
    {
        return Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count(l => l.Kind == PatchLineKind.Removed)));
    }
    
    /// <summary>
    /// Counts the total number of context lines across all files and hunks.
    /// </summary>
    public int CountContextLines()
    {
        return Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count(l => l.Kind == PatchLineKind.Context)));
    }
    
    /// <summary>
    /// Gets all added lines across all files and hunks.
    /// </summary>
    public IEnumerable<PatchLine> GetAllAddedLines()
    {
        return Files.SelectMany(f => f.Hunks.SelectMany(h => h.Lines.Where(l => l.Kind == PatchLineKind.Added)));
    }
    
    /// <summary>
    /// Gets all removed lines across all files and hunks.
    /// </summary>
    public IEnumerable<PatchLine> GetAllRemovedLines()
    {
        return Files.SelectMany(f => f.Hunks.SelectMany(h => h.Lines.Where(l => l.Kind == PatchLineKind.Removed)));
    }
}
