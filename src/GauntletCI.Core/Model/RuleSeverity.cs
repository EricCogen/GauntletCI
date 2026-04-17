// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

/// <summary>
/// Determines how a rule finding is treated during triage and CI/CD enforcement.
/// Severity is distinct from <see cref="Confidence"/>: confidence reflects how certain
/// the rule is that a pattern is present; severity reflects the impact if it is.
/// </summary>
public enum RuleSeverity
{
    /// <summary>Rule is disabled — findings are suppressed and the rule is not executed.</summary>
    None = 0,
    /// <summary>Informational — shown only with <c>--verbose</c>; never blocks a commit.</summary>
    Info = 1,
    /// <summary>Warning — shown by default; does not block unless <c>exitOn</c> is set to <c>Warn</c>.</summary>
    Warn = 2,
    /// <summary>Blocking — always shown; causes a non-zero exit code by default.</summary>
    Block = 3,
    /// <summary>Advisory — always shown; produced by LLM policy evaluation; never causes a non-zero exit code.</summary>
    Advisory = 4,
}
