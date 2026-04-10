// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Optional interface for rules that need access to GauntletConfig at evaluation time.
/// The orchestrator will call Configure() after instantiation.
/// </summary>
public interface IConfigurableRule : IRule
{
    void Configure(GauntletConfig config);
}
