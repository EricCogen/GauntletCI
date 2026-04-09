// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;

namespace GauntletCI.Cli.Presentation;

public static class CliBanner
{
    private const string Bold       = "\u001b[1m";
    private const string Amber      = "\u001b[38;5;214m";
    private const string Dim        = "\u001b[2m";
    private const string Reset      = "\u001b[0m";

    public static void PrintIfEnabled(BannerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (context.NoBanner) return;
        if (context.Quiet) return;
        if (!string.Equals(context.OutputFormat, "text", StringComparison.OrdinalIgnoreCase)) return;
        if (Console.IsOutputRedirected) return;
        if (IsCiEnvironment()) return;

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0] ?? "0.0.0";

        Console.WriteLine();
        Console.WriteLine($"  {Bold}{Amber}GauntletCI{Reset}  {Dim}v{version}{Reset}");
        Console.WriteLine($"  {Dim}pre-commit change-risk detection{Reset}");
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
