// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.MaintainerFetcher;

public sealed record MaintainerTarget(string Owner, string Repo, string[] Labels)
{
    public static readonly MaintainerTarget[] Defaults =
    [
        new("dotnet",  "runtime",     ["performance", "area-System.Runtime", "design-discussion"]),
        new("dotnet",  "roslyn",      ["performance", "design-discussion"]),
        new("aws",     "aws-sdk-net", ["performance", "design-discussion"]),
    ];
}
