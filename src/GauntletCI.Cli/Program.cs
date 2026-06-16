// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.LlmDaemon;
using GauntletCI.Cli.Telemetry;

// Hidden daemon mode: spawned by LlmDaemonClient, not intended for direct user invocation
if (args is ["__llm-daemon"])
{
    await LlmDaemonServer.RunAsync();
    return 0;
}

var isInitCommand = args.Any(a => string.Equals(a, "init", StringComparison.OrdinalIgnoreCase));
var isTelemetryCommand = args.Any(a => string.Equals(a, "telemetry", StringComparison.OrdinalIgnoreCase));
var isMcpServe = args.Any(a => string.Equals(a, "mcp", StringComparison.OrdinalIgnoreCase));
var isMetaInvocation = args.Length == 0
    || args.Any(static a => a is "--version" or "-?" or "-h" or "--help");

// First-run prompt for non-init paths (init handles its own prompt and supports --no-telemetry)
// Skip for mcp serve: stdout is the MCP protocol channel and must not be polluted
// Skip for bare/meta invocations (winget and package managers run the binary with no args or --version)
if (!isInitCommand && !isTelemetryCommand && !isMcpServe && !isMetaInvocation)
    TelemetryConsent.PromptIfNeeded();

var rootCommand = new RootCommand("GauntletCI: deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(AuditCommand.Create());
rootCommand.AddCommand(BaselineCommand.Create());
rootCommand.AddCommand(CorpusCommand.Create());
rootCommand.AddCommand(DoctorCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(IgnoreCommand.Create());
rootCommand.AddCommand(McpCommand.Create());
rootCommand.AddCommand(ModelCommand.Create());
rootCommand.AddCommand(LicenseCommand.Create());
rootCommand.AddCommand(LlmCommand.Create());
rootCommand.AddCommand(PostmortemCommand.Create());
rootCommand.AddCommand(TraceCommand.Create());
rootCommand.AddCommand(FeedbackCommand.Create());
rootCommand.AddCommand(TelemetryCommand.Create());

if (args.Length == 0)
    return await rootCommand.InvokeAsync(["--help"]);

return await rootCommand.InvokeAsync(args);
