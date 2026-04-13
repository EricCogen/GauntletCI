// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.FileAnalysis;

public enum FileEligibilityClassification
{
    EligibleSource      = 0,
    KnownNonSource      = 1,
    UnknownUnsupported  = 2,
    Binary              = 3,
    Generated           = 4,
    Deleted             = 5,
    RenamedOnly         = 6,
    EmptyPath           = 7,
    MissingExtension    = 8
}
