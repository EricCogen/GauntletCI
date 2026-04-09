// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.LlmDaemon;
using GauntletCI.Cli.Telemetry;

// Hidden daemon mode — spawned by LlmDaemonClient, not intended for direct user invocation
if (args is ["__llm-daemon"])
{
    await LlmDaemonServer.RunAsync();
    return 0;
}

// First-run opt-in prompt (skipped in CI / redirected contexts)
TelemetryConsent.PromptIfNeeded();

var rootCommand = new RootCommand("GauntletCI — deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(IgnoreCommand.Create());
rootCommand.AddCommand(ModelCommand.Create());
rootCommand.AddCommand(PostmortemCommand.Create());
rootCommand.AddCommand(FeedbackCommand.Create());
rootCommand.AddCommand(TelemetryCommand.Create());

return await rootCommand.InvokeAsync(args);
