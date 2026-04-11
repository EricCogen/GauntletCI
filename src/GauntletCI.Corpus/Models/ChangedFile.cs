// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class ChangedFile
{
    public string Path { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public string Patch { get; init; } = string.Empty;
    public bool IsTestFile { get; init; }
    public string LanguageHint { get; init; } = string.Empty;
}
