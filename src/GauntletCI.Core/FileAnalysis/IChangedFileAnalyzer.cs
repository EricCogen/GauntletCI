// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.FileAnalysis;

public interface IChangedFileAnalyzer
{
    ChangedFileAnalysisRecord Analyze(DiffFile file);
}
