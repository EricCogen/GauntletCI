// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Mcp;
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
        var cmd = new Command("serve", "Start the GauntletCI MCP server over stdio") { repoOption };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            Console.Error.WriteLine("[mcp] GauntletCI MCP server running (stdio)");
            Console.Error.WriteLine("[mcp] Add to Claude Desktop: { \"mcpServers\": { \"gauntletci\": { \"command\": \"gauntletci\", \"args\": [\"mcp\", \"serve\"] } } }");

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
