// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// gauntletci feedback up|down
/// Records a thumbs-up or thumbs-down on the most recent analysis.
/// Stored as an anonymous telemetry event and uploaded with the next batch.
/// </summary>
public static class FeedbackCommand
{
    public static Command Create()
    {
        var voteArg = new Argument<string>("vote", "up or down") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("feedback", "Rate the quality of the last analysis (up = useful, down = too noisy)")
        {
            voteArg,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var vote = ctx.ParseResult.GetValueForArgument(voteArg).ToLowerInvariant();

            if (vote is not ("up" or "down"))
            {
                Console.Error.WriteLine("[GauntletCI] Vote must be 'up' or 'down'.");
                ctx.ExitCode = 1;
                return;
            }

            if (!TelemetryConsent.HasDecided)
            {
                Console.WriteLine("  Telemetry is not enabled. Run 'gauntletci telemetry --enable' to opt in.");
                ctx.ExitCode = 0;
                return;
            }

            if (!TelemetryConsent.IsOptedIn)
            {
                Console.WriteLine("  Feedback requires telemetry to be enabled.");
                Console.WriteLine("  Run 'gauntletci telemetry --enable' to opt in.");
                ctx.ExitCode = 0;
                return;
            }

            await TelemetryStore.AppendAsync(new TelemetryEvent
            {
                EventType = "feedback",
                InstallId = TelemetryConsent.InstallId,
                Vote      = vote,
            });

            TelemetryUploader.UploadInBackground();

            var emoji = vote == "up" ? "👍" : "👎";
            Console.WriteLine($"  {emoji}  Feedback recorded — thank you!");
            ctx.ExitCode = 0;
        });

        return cmd;
    }
}
