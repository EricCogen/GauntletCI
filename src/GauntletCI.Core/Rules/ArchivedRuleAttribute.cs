// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules;

/// <summary>
/// Marks a rule as archived. Archived rules are excluded from <see cref="RuleOrchestrator.CreateDefault"/>
/// auto-discovery but remain in the assembly so their IDs, tests, and corpus labels are preserved.
/// An archived rule can still be instantiated directly or re-enabled via explicit construction.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ArchivedRuleAttribute : Attribute
{
    /// <summary>The reason this rule was archived.</summary>
    public string Reason { get; }

    public ArchivedRuleAttribute(string reason)
    {
        Reason = reason;
    }
}
