// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.MaintainerFetcher;

public sealed class MaintainerRecord
{
    public string Owner     { get; init; } = "";
    public string Repo      { get; init; } = "";
    public int    Number    { get; init; }
    public string Type      { get; init; } = ""; // "pr" | "issue"
    public string Author    { get; init; } = "";
    public string Title     { get; init; } = "";
    public string Body      { get; init; } = "";
    public string[] Labels  { get; init; } = [];
    public string Url       { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public int Reactions    { get; init; }
}
