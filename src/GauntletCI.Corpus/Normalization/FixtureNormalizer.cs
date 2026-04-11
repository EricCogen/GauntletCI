// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Normalization;

/// <summary>
/// Converts a <see cref="HydratedPullRequest"/> into a <see cref="FixtureMetadata"/>
/// ready for storage. Normalization is deterministic and idempotent.
/// </summary>
public static class FixtureNormalizer
{
    private static readonly string[] AsyncTags           = ["async", "await", ".Result", ".Wait("];
    private static readonly string[] ContractChangeTags  = ["public ", "interface ", "abstract "];
    private static readonly string[] EarlyReturnTags     = ["return ", "throw "];
    private static readonly string[] NullSafetyTags      = ["null", "?.","??"];
    private static readonly string[] StateMutationTags   = ["= new ", "List<", "Dictionary<", "HashSet<"];
    private static readonly string[] LoggingTags         = ["_logger", "Log.", "ILogger"];
    private static readonly string[] ExceptionFlowTags   = ["catch ", "throw ", "Exception"];

    public static FixtureMetadata Normalize(HydratedPullRequest pr, string source = "manual")
    {
        var fixtureId = FixtureIdHelper.Build(pr.RepoOwner, pr.RepoName, pr.PullRequestNumber);
        var tags      = InferTags(pr);
        var language  = InferLanguage(pr.ChangedFiles);

        return new FixtureMetadata
        {
            FixtureId         = fixtureId,
            Tier              = FixtureTier.Discovery,
            Repo              = $"{pr.RepoOwner}/{pr.RepoName}",
            PullRequestNumber = pr.PullRequestNumber,
            Language          = language,
            RuleIds           = [],
            Tags              = tags,
            PrSizeBucket      = PrSizeBucketClassifier.Classify(pr.FilesChangedCount),
            FilesChanged      = pr.FilesChangedCount,
            HasTestsChanged   = pr.ChangedFiles.Any(f => f.IsTestFile),
            HasReviewComments = pr.ReviewComments.Count > 0,
            BaseSha           = pr.BaseSha,
            HeadSha           = pr.HeadSha,
            Source            = source,
            CreatedAtUtc      = pr.HydratedAtUtc,
        };
    }

    private static IReadOnlyList<string> InferTags(HydratedPullRequest pr)
    {
        var diff = pr.DiffText;
        var tags = new HashSet<string>(StringComparer.Ordinal);

        if (HasAny(diff, AsyncTags))          tags.Add("async");
        if (HasAny(diff, ContractChangeTags)) tags.Add("contract-change");
        if (HasAny(diff, EarlyReturnTags))    tags.Add("early-return");
        if (HasAny(diff, NullSafetyTags))     tags.Add("null-safety");
        if (HasAny(diff, StateMutationTags))  tags.Add("state-mutation");
        if (HasAny(diff, LoggingTags))        tags.Add("logging");
        if (HasAny(diff, ExceptionFlowTags))  tags.Add("exception-flow");

        if (pr.ChangedFiles.Any(f =>
            f.Patch.Contains("public ") && (f.Patch.Contains("(") || f.Patch.Contains("{"))))
            tags.Add("api-change");

        return [.. tags.Order()];
    }

    private static string InferLanguage(IReadOnlyList<ChangedFile> files)
    {
        var counts = files
            .Where(f => !string.IsNullOrEmpty(f.LanguageHint))
            .GroupBy(f => f.LanguageHint)
            .OrderByDescending(g => g.Count());

        return counts.FirstOrDefault()?.Key ?? "";
    }

    private static bool HasAny(string text, string[] tokens)
        => tokens.Any(t => text.Contains(t, StringComparison.Ordinal));
}
