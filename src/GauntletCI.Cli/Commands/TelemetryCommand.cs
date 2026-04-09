// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// gauntletci telemetry [--status | --enable | --disable]
/// Manage opt-in telemetry consent without re-running an analysis.
/// </summary>
public static class TelemetryCommand
{
    public static Command Create()
    {
        var statusFlag  = new Option<bool>("--status",  "Show current telemetry status");
        var enableFlag  = new Option<bool>("--enable",  "Opt in to anonymous telemetry");
        var disableFlag = new Option<bool>("--disable", "Opt out of telemetry");

        var cmd = new Command("telemetry", "Manage anonymous telemetry preferences")
        {
            statusFlag,
            enableFlag,
            disableFlag,
        };

        cmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var status  = ctx.ParseResult.GetValueForOption(statusFlag);
            var enable  = ctx.ParseResult.GetValueForOption(enableFlag);
            var disable = ctx.ParseResult.GetValueForOption(disableFlag);

            if (enable)
            {
                TelemetryConsent.SetOptIn(true);
                Console.WriteLine("  ✓ Anonymous telemetry enabled. Thank you!");
                return;
            }

            if (disable)
            {
                TelemetryConsent.SetOptIn(false);
                Console.WriteLine("  ✓ Telemetry disabled. No data will be collected or sent.");
                return;
            }

            // Default: show status
            var decided = TelemetryConsent.HasDecided;
            var optedIn = TelemetryConsent.IsOptedIn;
            var installId = TelemetryConsent.InstallId;

            Console.WriteLine();
            Console.WriteLine($"  Install ID : {installId}");
            Console.WriteLine($"  Status     : {(decided ? (optedIn ? "Enabled ✓" : "Disabled") : "Not yet decided")}");
            Console.WriteLine();
            Console.WriteLine("  GauntletCI collects anonymous rule-fire metrics only.");
            Console.WriteLine("  No code, no file paths, no identifiers ever leave your machine unprocessed.");
            Console.WriteLine();
            Console.WriteLine("  To change:  gauntletci telemetry --enable | --disable");
            Console.WriteLine();
            ctx.ExitCode = 0;

            return;
        });

        return cmd;
    }
}
