// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Licensing;
using GauntletCI.Core.Licensing;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// Implements <c>gauntletci license status</c> and <c>gauntletci license renew</c>.
/// </summary>
public static class LicenseCommand
{
    public static Command Create()
    {
        var cmd = new Command("license", "Inspect the active GauntletCI license");
        cmd.AddCommand(CreateStatusCommand());
        cmd.AddCommand(CreateRenewCommand());
        return cmd;
    }

    private static Command CreateStatusCommand()
    {
        var offlineFlag = new Option<bool>("--offline", "Skip remote subscription check (for air-gapped environments)");
        var statusCmd = new Command("status", "Show license tier, validity, and subscription status");
        statusCmd.AddOption(offlineFlag);

        statusCmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var offline = ctx.ParseResult.GetValueForOption(offlineFlag);
            if (offline)
                Environment.SetEnvironmentVariable("GAUNTLETCI_OFFLINE", "1");

            const string EnvVar = "GAUNTLETCI_LICENSE";
            var license  = LicenseService.Load(EnvVar);
            var rawToken = LicenseService.ReadRawToken(EnvVar);

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

            // Remote subscription check for paid tiers.
            if (license.IsValid && license.Tier > LicenseTier.Community && rawToken is not null)
            {
                AnsiConsole.WriteLine();
                var (netValid, reason) = await NetworkLicenseValidator.ValidateAsync(
                    rawToken, ctx.GetCancellationToken());

                if (netValid)
                    AnsiConsole.MarkupLine("  Subscription: [green]Active[/]");
                else
                    AnsiConsole.MarkupLine($"  Subscription: [red]Inactive[/] ({Markup.Escape(reason ?? "cancelled")})");
            }

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

        return statusCmd;
    }

    private static Command CreateRenewCommand()
    {
        var renewCmd = new Command("renew", "Instructions for renewing or replacing your license token");

        renewCmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            AnsiConsole.MarkupLine("[bold cyan]GauntletCI License Renewal[/]");
            AnsiConsole.MarkupLine("[dim]---------------------------------------------------[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Your license token is reissued automatically when your Stripe");
            AnsiConsole.MarkupLine("subscription renews. Check your email for the latest token.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]To replace your token:[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [cyan]Option 1[/] -- update the key file:");
            AnsiConsole.MarkupLine("    echo '<new-token>' > ~/.gauntletci/gauntletci.key");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [cyan]Option 2[/] -- update the environment variable:");
            AnsiConsole.MarkupLine("    export GAUNTLETCI_LICENSE='<new-token>'");
            AnsiConsole.MarkupLine("    (Update your CI/CD secret with the same value.)");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [cyan]Option 3[/] -- purchase a new subscription:");
            AnsiConsole.MarkupLine("    https://gauntletci.com/pricing");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Need help? Email support@gauntletci.com[/]");
            ctx.ExitCode = 0;
        });

        return renewCmd;
    }
}

