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

var isInitCommand = args.Any(a => string.Equals(a, "init", StringComparison.OrdinalIgnoreCase));
var isTelemetryCommand = args.Any(a => string.Equals(a, "telemetry", StringComparison.OrdinalIgnoreCase));
var isMcpServe = args.Any(a => string.Equals(a, "mcp", StringComparison.OrdinalIgnoreCase));

// First-run prompt for non-init paths (init handles its own prompt and supports --no-telemetry)
// Skip for mcp serve — stdout is the MCP protocol channel and must not be polluted
if (!isInitCommand && !isTelemetryCommand && !isMcpServe)
    TelemetryConsent.PromptIfNeeded();

var rootCommand = new RootCommand("GauntletCI — deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(AuditCommand.Create());
rootCommand.AddCommand(CorpusCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(IgnoreCommand.Create());
rootCommand.AddCommand(McpCommand.Create());
rootCommand.AddCommand(ModelCommand.Create());
rootCommand.AddCommand(PostmortemCommand.Create());
rootCommand.AddCommand(FeedbackCommand.Create());
rootCommand.AddCommand(TelemetryCommand.Create());

return await rootCommand.InvokeAsync(args);
