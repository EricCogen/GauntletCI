// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Mcp;
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
            var ollamaModel = ctx.ParseResult.GetValueForOption(ollamaModelOption);
            var ollamaUrl = ctx.ParseResult.GetValueForOption(ollamaUrlOption)!;

            if (ollamaModel is not null)
            {
                var endpoint = $"{ollamaUrl.TrimEnd('/')}/v1/chat/completions";
                GauntletTools.SetEngine(new RemoteLlmEngine(endpoint, ollamaModel, "ollama"));
                Console.Error.WriteLine($"[mcp] LLM enrichment enabled: Ollama model '{ollamaModel}' at {ollamaUrl}");
                Console.Error.WriteLine("[mcp] High-confidence findings will include llmExplanation in responses.");
            }

            Console.Error.WriteLine("[mcp] GauntletCI MCP server running (stdio)");
            Console.Error.WriteLine("[mcp] Add to Claude Desktop: { \"mcpServers\": { \"gauntletci\": { \"command\": \"gauntletci\", \"args\": [\"mcp\", \"serve\", \"--ollama-model\", \"phi3\"] } } }");

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

            await builder.Build().RunAsync(ctx.GetCancellationToken());
        });

        return cmd;
    }
}

