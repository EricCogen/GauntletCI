// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Licensing;
using GauntletCI.Cli.Mcp;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Licensing;
using GauntletCI.Core.Security;
using GauntletCI.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace GauntletCI.Cli.Commands;

public static class McpCommand
{
    public static Command Create()
    {
        var mcpCommand = new Command("mcp", "Model Context Protocol server integration");
        mcpCommand.AddCommand(CreateServe());
        return mcpCommand;
    }

    private static Command CreateServe()
    {
        var repoOption = new Option<string?>("--repo", "Repository root path (defaults to current directory)");
        var ollamaModelOption = new Option<string?>(
            "--ollama-model",
            "Ollama model name to use for LLM enrichment of findings (e.g. phi3, llama3.2). Omit to disable enrichment.");
        var ollamaUrlOption = new Option<string>(
            "--ollama-url",
            () => "http://localhost:11434",
            "Ollama base URL");

        var cmd = new Command("serve", "Start the GauntletCI MCP server over stdio")
        {
            repoOption,
            ollamaModelOption,
            ollamaUrlOption,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var repoPath = ctx.ParseResult.GetValueForOption(repoOption);
            var ollamaModel = ctx.ParseResult.GetValueForOption(ollamaModelOption);
            var ollamaUrl = ctx.ParseResult.GetValueForOption(ollamaUrlOption)!;
            var ct = ctx.GetCancellationToken();

            var repoRoot = repoPath ?? Directory.GetCurrentDirectory();
            var config = ConfigLoader.Load(repoRoot);
            var licenseEnvVar = config.Llm?.LicenseKeyEnv ?? "GAUNTLETCI_LICENSE";
            var licenseExit = await PaidFeatureGate.TryEnsureTierAsync(
                LicenseTier.Pro, "MCP server", licenseEnvVar, ct);
            if (licenseExit is int code)
            {
                ctx.ExitCode = code;
                return;
            }

            if (ollamaModel is not null)
            {
                if (!LlmEndpointValidator.TryValidateMcpOllamaBaseUrl(ollamaUrl, out var ollamaError))
                {
                    Console.Error.WriteLine($"[GauntletCI] MCP Ollama URL rejected: {ollamaError}");
                    ctx.ExitCode = 2;
                    return;
                }

                var endpoint = $"{ollamaUrl.TrimEnd('/')}/v1/chat/completions";
                GauntletTools.SetEngine(new RemoteLlmEngine(endpoint, ollamaModel, "ollama"));
                Console.Error.WriteLine($"[mcp] LLM enrichment enabled: Ollama model '{ollamaModel}' at {ollamaUrl}");
                Console.Error.WriteLine("[mcp] High-confidence findings will include llmExplanation in responses.");
            }

            Console.Error.WriteLine("[mcp] GauntletCI MCP server running (stdio)");
            Console.Error.WriteLine("[mcp] Add to Claude Desktop: { \"mcpServers\": { \"gauntletci\": { \"command\": \"gauntletci\", \"args\": [\"mcp\", \"serve\", \"--ollama-model\", \"phi4-mini\"] } } }");

            var builder = Host.CreateApplicationBuilder();
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(typeof(GauntletTools).Assembly);

            await builder.Build().RunAsync(ct);
        });

        return cmd;
    }
}

