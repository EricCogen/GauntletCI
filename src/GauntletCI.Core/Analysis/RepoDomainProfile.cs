// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Analysis;

/// <summary>Result of <see cref="RepoDomainClassifier"/> for the current evaluation.</summary>
public sealed class RepoDomainProfile
{
    public RepoDomainKind Kind { get; init; } = RepoDomainKind.Unknown;

    /// <summary>Human-readable explanation of how the profile was resolved.</summary>
    public string Reason { get; init; } = string.Empty;

    public bool IsClassLibrary => Kind == RepoDomainKind.ClassLibrary;

    public bool IsWebApplication => Kind == RepoDomainKind.WebApplication;
}
