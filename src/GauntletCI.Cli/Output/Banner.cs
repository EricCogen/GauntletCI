// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;

namespace GauntletCI.Cli.Output;

/// <summary>
/// Prints the GauntletCI startup banner. Suppressed by --no-banner or GAUNTLETCI_NO_BANNER=1.
/// </summary>
public static class Banner
{
    private static readonly string Version =
        typeof(Banner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? "2.0.0";

    private const string UnicodeShield = """
          ╔══════════════════════════════════════════════╗
          ║  ⚔  GauntletCI {0,-28}║
          ║     Pre-commit risk detection                ║
          ║                                              ║
          ║  "You changed what the code does.           ║
          ║   Nothing proves it still works."           ║
          ╚══════════════════════════════════════════════╝
        """;

    private const string AsciiShield = """
          +================================================+
          |  [GCI]  GauntletCI {0,-26}|
          |         Pre-commit risk detection              |
          |                                                |
          |  "You changed what the code does.             |
          |   Nothing proves it still works."             |
          +================================================+
        """;

    public static void Print(bool ascii = false, bool suppress = false)
    {
        if (suppress || IsSuppressed()) return;

        var template = ascii ? AsciiShield : UnicodeShield;
        var versionLabel = $"v{Version}";
        var banner = string.Format(template, versionLabel);

        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(banner);
        Console.ForegroundColor = prev;
    }

    private static bool IsSuppressed()
    {
        var env = Environment.GetEnvironmentVariable("GAUNTLETCI_NO_BANNER");
        return env is "1" or "true";
    }
}
