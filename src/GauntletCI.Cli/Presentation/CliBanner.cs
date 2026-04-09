// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Cli.Presentation;

public static class CliBanner
{
    private const string BannerText =
        " ██████╗  █████╗ ██╗   ██╗███╗   ██╗████████╗██╗     ███████╗████████╗ ██████╗ ██╗\n" +
        "██╔════╝ ██╔══██╗██║   ██║████╗  ██║╚══██╔══╝██║     ██╔════╝╚══██╔══╝██╔════╝ ██║\n" +
        "██║  ███╗███████║██║   ██║██╔██╗ ██║   ██║   ██║     █████╗     ██║   ██║      ██║\n" +
        "██║   ██║██╔══██║██║   ██║██║╚██╗██║   ██║   ██║     ██╔══╝     ██║   ██║      ██║\n" +
        "╚██████╔╝██║  ██║╚██████╔╝██║ ╚████║   ██║   ███████╗███████╗   ██║   ╚██████╗ ██║\n" +
        " ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝   ╚═╝   ╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝\n" +
        "\n" +
        "GauntletCI - pre-commit change-risk detection";

    public static void PrintIfEnabled(BannerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (context.NoBanner) return;
        if (context.Quiet) return;
        if (!string.Equals(context.OutputFormat, "text", StringComparison.OrdinalIgnoreCase)) return;
        if (Console.IsOutputRedirected) return;
        if (IsCiEnvironment()) return;

        Console.WriteLine(BannerText.Replace("\r\n", "\n").Replace("\r", "\n"));
        Console.WriteLine();
    }

    private static bool IsCiEnvironment()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TF_BUILD")))
            return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILD_BUILDID")))
            return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JENKINS_URL")))
            return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GAUNTLETCI_NO_BANNER")))
            return true;

        return false;
    }
}
