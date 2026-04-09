// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.Telemetry;

// First-run opt-in prompt (skipped in CI / redirected contexts)
TelemetryConsent.PromptIfNeeded();

var rootCommand = new RootCommand("GauntletCI — deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(IgnoreCommand.Create());
rootCommand.AddCommand(PostmortemCommand.Create());
rootCommand.AddCommand(FeedbackCommand.Create());
rootCommand.AddCommand(TelemetryCommand.Create());

return await rootCommand.InvokeAsync(args);
