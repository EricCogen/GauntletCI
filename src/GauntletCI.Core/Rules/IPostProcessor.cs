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
    Finding? PostProcess(DiffContext context);
}
