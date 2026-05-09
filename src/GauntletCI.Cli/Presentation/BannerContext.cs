// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Cli.Presentation;

public sealed class BannerContext
{
    public bool NoBanner { get; init; }
    public bool Quiet { get; init; }
    public string OutputFormat { get; init; } = "text";
}
