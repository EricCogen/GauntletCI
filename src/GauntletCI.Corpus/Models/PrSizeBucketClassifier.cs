// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public static class PrSizeBucketClassifier
{
    public static PrSizeBucket Classify(int filesChanged) => filesChanged switch
    {
        <= 2 => PrSizeBucket.Tiny,
        <= 7 => PrSizeBucket.Small,
        <= 20 => PrSizeBucket.Medium,
        <= 75 => PrSizeBucket.Large,
        _ => PrSizeBucket.Huge,
    };
}
