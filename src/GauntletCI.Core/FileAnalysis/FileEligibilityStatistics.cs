// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.FileAnalysis;

public sealed class FileEligibilityStatistics
{
    public int TotalFiles { get; init; }
    public int EligibleFiles { get; init; }
    public int SkippedFiles { get; init; }

    public int EligibleSourceCount { get; init; }
    public int KnownNonSourceCount { get; init; }
    public int UnknownUnsupportedCount { get; init; }
    public int BinaryCount { get; init; }
    public int GeneratedCount { get; init; }
    public int DeletedCount { get; init; }
    public int RenamedOnlyCount { get; init; }
    public int EmptyPathCount { get; init; }
    public int MissingExtensionCount { get; init; }

    public static FileEligibilityStatistics From(IReadOnlyList<ChangedFileAnalysisRecord> records)
    {
        return new FileEligibilityStatistics
        {
            TotalFiles              = records.Count,
            EligibleFiles           = records.Count(r => r.IsEligible),
            SkippedFiles            = records.Count(r => !r.IsEligible),
            EligibleSourceCount     = records.Count(r => r.Classification == FileEligibilityClassification.EligibleSource),
            KnownNonSourceCount     = records.Count(r => r.Classification == FileEligibilityClassification.KnownNonSource),
            UnknownUnsupportedCount = records.Count(r => r.Classification == FileEligibilityClassification.UnknownUnsupported),
            BinaryCount             = records.Count(r => r.Classification == FileEligibilityClassification.Binary),
            GeneratedCount          = records.Count(r => r.Classification == FileEligibilityClassification.Generated),
            DeletedCount            = records.Count(r => r.Classification == FileEligibilityClassification.Deleted),
            RenamedOnlyCount        = records.Count(r => r.Classification == FileEligibilityClassification.RenamedOnly),
            EmptyPathCount          = records.Count(r => r.Classification == FileEligibilityClassification.EmptyPath),
            MissingExtensionCount   = records.Count(r => r.Classification == FileEligibilityClassification.MissingExtension),
        };
    }
}
