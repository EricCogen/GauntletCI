// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.FileAnalysis;

/// <summary>Describes why a changed file was included in or excluded from rule analysis.</summary>
public enum FileEligibilityClassification
{
    /// <summary>The file is a recognized source file and will be analyzed by rules.</summary>
    EligibleSource      = 0,
    /// <summary>The file has a known non-source extension (e.g. images, docs) and is skipped.</summary>
    KnownNonSource      = 1,
    /// <summary>The file extension is not in the supported allowlist and is skipped.</summary>
    UnknownUnsupported  = 2,
    /// <summary>The file appears to be a binary and cannot be analyzed as text.</summary>
    Binary              = 3,
    /// <summary>The file is auto-generated code (e.g. <c>.g.cs</c>, <c>.designer.cs</c>) and is skipped.</summary>
    Generated           = 4,
    /// <summary>The file was deleted by this commit and is not analyzed.</summary>
    Deleted             = 5,
    /// <summary>The file was renamed without content changes and is skipped in v1.</summary>
    RenamedOnly         = 6,
    /// <summary>The parsed diff produced an empty file path, indicating a malformed diff entry.</summary>
    EmptyPath           = 7,
    /// <summary>The file has no extension and cannot be classified.</summary>
    MissingExtension    = 8
}
