// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Core.Licensing;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// Implements <c>gauntletci license status</c> -- inspects the active license.
/// </summary>
public static class LicenseCommand
{
    public static Command Create()
    {
        var cmd = new Command("license", "Inspect the active GauntletCI license");

        var statusCmd = new Command("status", "Show license tier, validity, and expiry");
        statusCmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            const string EnvVar = "GAUNTLETCI_LICENSE";
            var license = LicenseService.Load(EnvVar);

            AnsiConsole.MarkupLine("[bold cyan]GauntletCI License[/]");
            AnsiConsole.MarkupLine("[dim]---------------------------------------------------[/]");
            AnsiConsole.WriteLine();

            var tierColor = license.Tier switch
            {
                LicenseTier.Community  => "dim",
                LicenseTier.Pro        => "cyan",
                LicenseTier.Teams      => "green",
                LicenseTier.Enterprise => "yellow",
                _                      => "dim",
            };

            AnsiConsole.MarkupLine($"  Tier    : [{tierColor}]{license.Tier}[/]");
            AnsiConsole.MarkupLine($"  Valid   : {(license.IsValid ? "[green]Yes[/]" : "[red]No[/]")}");

            if (license.Email is not null)
                AnsiConsole.MarkupLine($"  Email   : {Markup.Escape(license.Email)}");

            if (license.ExpiresAt.HasValue)
                AnsiConsole.MarkupLine($"  Expires : {license.ExpiresAt.Value:yyyy-MM-dd}");
            else if (license.IsValid && license.Tier > LicenseTier.Community)
                AnsiConsole.MarkupLine("  Expires : [dim]never[/]");

            if (license.Error is not null)
                AnsiConsole.MarkupLine($"  [yellow]Notice  : {Markup.Escape(license.Error)}[/]");

            AnsiConsole.WriteLine();

            if (!license.IsValid)
            {
                AnsiConsole.MarkupLine("[dim]Get a license at https://gauntletci.com/pricing[/]");
                AnsiConsole.MarkupLine($"[dim]Place it at ~/.gauntletci/gauntletci.key or set {EnvVar}[/]");
                ctx.ExitCode = 1;
            }
            else if (license.Tier == LicenseTier.Community)
            {
                AnsiConsole.MarkupLine("[dim]Running on Community tier. Upgrade at https://gauntletci.com/pricing[/]");
                AnsiConsole.MarkupLine($"[dim]Place license at ~/.gauntletci/gauntletci.key or set {EnvVar}[/]");
                ctx.ExitCode = 0;
            }
            else
            {
                ctx.ExitCode = 0;
            }
        });

        cmd.AddCommand(statusCmd);
        return cmd;
    }
}
