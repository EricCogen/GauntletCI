// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public enum FixtureTier { Discovery, Silver, Gold }

public enum PrSizeBucket { Tiny, Small, Medium, Large, Huge }

public enum MergeState { Open, Merged, Closed }

public enum LabelSource { Heuristic, FilePathCorrelation, HumanReview, Seed, LlmReview }
