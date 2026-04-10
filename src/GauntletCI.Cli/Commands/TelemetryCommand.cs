// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// gauntletci telemetry [--status | --mode shared|local|off | --enable | --disable]
/// Manage telemetry preferences without re-running an analysis.
/// </summary>
public static class TelemetryCommand
{
    public static Command Create()
    {
        var statusFlag  = new Option<bool>("--status", "Show current telemetry status");
        var modeOption = new Option<string?>("--mode", "Set telemetry mode: shared, local, or off");
        var enableFlag  = new Option<bool>("--enable", "Opt in to shared telemetry (alias for --mode shared)");
        var disableFlag = new Option<bool>("--disable", "Disable telemetry (alias for --mode off)");

        var cmd = new Command("telemetry", "Manage telemetry preferences")
        {
            statusFlag,
            modeOption,
            enableFlag,
            disableFlag,
        };

        cmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var mode = ctx.ParseResult.GetValueForOption(modeOption);
            var enable  = ctx.ParseResult.GetValueForOption(enableFlag);
            var disable = ctx.ParseResult.GetValueForOption(disableFlag);

            if (enable)
            {
                TelemetryConsent.SetMode(TelemetryMode.Shared);
                Console.WriteLine("  ✓ Telemetry mode set to shared.");
                return;
            }

            if (disable)
            {
                TelemetryConsent.SetMode(TelemetryMode.Off);
                Console.WriteLine("  ✓ Telemetry mode set to off.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(mode))
            {
                var parsed = mode.Trim().ToLowerInvariant() switch
                {
                    "shared" => TelemetryMode.Shared,
                    "local" => TelemetryMode.Local,
                    "off" => TelemetryMode.Off,
                    _ => (TelemetryMode?)null,
                };

                if (parsed is null)
                {
                    Console.Error.WriteLine("  Invalid mode. Use: shared, local, or off.");
                    ctx.ExitCode = 1;
                    return;
                }

                TelemetryConsent.SetMode(parsed.Value);
                Console.WriteLine($"  ✓ Telemetry mode set to {mode.Trim().ToLowerInvariant()}.");
                return;
            }

            var currentMode = TelemetryConsent.GetMode();
            var installId = TelemetryConsent.InstallId;

            Console.WriteLine();
            Console.WriteLine($"  Install ID : {installId}");
            Console.WriteLine($"  Mode       : {currentMode.ToString().ToLowerInvariant()}");
            Console.WriteLine();
            Console.WriteLine("  shared = local store + anonymous aggregate upload");
            Console.WriteLine("  local  = local store only, no network calls");
            Console.WriteLine("  off    = telemetry disabled");
            Console.WriteLine();
            Console.WriteLine("  To change: gauntletci telemetry --mode shared|local|off");
            Console.WriteLine();
            ctx.ExitCode = 0;
        });

        return cmd;
    }
}
