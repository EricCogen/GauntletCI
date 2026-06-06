// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Optional interface for rule evaluators that need to run a post-processing
/// step after all rules have evaluated (e.g. adding synthetic findings based
/// on aggregate diff properties).
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Runs after all rules have evaluated. Returns an optional synthetic finding
    /// based on aggregate diff properties and prior rule output.
    /// </summary>
    Finding? PostProcess(DiffContext context, IReadOnlyList<Finding> findings);
}
