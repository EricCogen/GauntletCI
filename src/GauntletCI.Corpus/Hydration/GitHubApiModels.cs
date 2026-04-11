// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json.Serialization;

namespace GauntletCI.Corpus.Hydration;

// Lightweight internal DTOs for deserializing GitHub REST API responses.
// Only fields used by the hydrator are mapped; extras are silently ignored.

internal sealed class GhPullRequest
{
    [JsonPropertyName("number")]    public int Number { get; init; }
    [JsonPropertyName("title")]     public string Title { get; init; } = "";
    [JsonPropertyName("body")]      public string? Body { get; init; }
    [JsonPropertyName("state")]     public string State { get; init; } = "";
    [JsonPropertyName("draft")]     public bool Draft { get; init; }
    [JsonPropertyName("additions")] public int Additions { get; init; }
    [JsonPropertyName("deletions")] public int Deletions { get; init; }
    [JsonPropertyName("changed_files")] public int ChangedFiles { get; init; }
    [JsonPropertyName("merge_commit_sha")] public string? MergeCommitSha { get; init; }
    [JsonPropertyName("base")]      public GhRef Base { get; init; } = new();
    [JsonPropertyName("head")]      public GhRef Head { get; init; } = new();
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; init; }
}

internal sealed class GhRef
{
    [JsonPropertyName("sha")] public string Sha { get; init; } = "";
}

internal sealed class GhFile
{
    [JsonPropertyName("filename")]  public string Filename { get; init; } = "";
    [JsonPropertyName("status")]    public string Status { get; init; } = "";
    [JsonPropertyName("additions")] public int Additions { get; init; }
    [JsonPropertyName("deletions")] public int Deletions { get; init; }
    [JsonPropertyName("patch")]     public string? Patch { get; init; }
}

internal sealed class GhReviewComment
{
    [JsonPropertyName("user")]       public GhUser User { get; init; } = new();
    [JsonPropertyName("body")]       public string Body { get; init; } = "";
    [JsonPropertyName("path")]       public string Path { get; init; } = "";
    [JsonPropertyName("diff_hunk")]  public string DiffHunk { get; init; } = "";
    [JsonPropertyName("position")]   public int? Position { get; init; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("html_url")]   public string HtmlUrl { get; init; } = "";
}

internal sealed class GhUser
{
    [JsonPropertyName("login")] public string Login { get; init; } = "";
}

internal sealed class GhCommit
{
    [JsonPropertyName("sha")] public string Sha { get; init; } = "";
}
