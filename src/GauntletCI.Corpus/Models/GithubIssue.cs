// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class GithubIssue
{
    public string Id { get; init; } = string.Empty;   // "github:{owner}/{repo}#{number}"
    public string RepoOwner { get; init; } = string.Empty;
    public string RepoName { get; init; } = string.Empty;
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public List<string> Labels { get; init; } = [];
    public string State { get; init; } = string.Empty;
    public DateTime? ClosedAtUtc { get; init; }
    public string Url { get; init; } = string.Empty;
}
