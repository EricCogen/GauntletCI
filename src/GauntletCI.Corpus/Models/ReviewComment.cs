// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class ReviewComment
{
    public string Author { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string DiffHunk { get; init; } = string.Empty;
    public int Position { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string Url { get; init; } = string.Empty;
}
