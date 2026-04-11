// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Interfaces;

public interface IReportExporter
{
    Task<string> ExportMarkdownAsync(CancellationToken cancellationToken = default);
}
