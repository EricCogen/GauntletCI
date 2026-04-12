// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Llm;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

public static class ModelCommand
{
    private static readonly string DefaultModelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "models", "phi3-mini");

    public static Command Create()
    {
        var cmd = new Command("model", "Manage the local LLM model used for finding enrichment");
        cmd.AddCommand(CreateDownload());
        cmd.AddCommand(CreateStatus());
        return cmd;
    }

    private static Command CreateDownload()
    {
        var dirOption = new Option<string>(
            "--dir",
            () => DefaultModelDir,
            "Directory to download the model into");

        var cmd = new Command("download", "Download the Phi-3 Mini INT4 ONNX model (~2 GB) for offline enrichment")
        {
            dirOption,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var dir = ctx.ParseResult.GetValueForOption(dirOption)!;
            var downloader = new ModelDownloader(dir);
            var progress = new Progress<string>(msg => AnsiConsole.MarkupLine($"[dim]{Markup.Escape(msg)}[/]"));

            try
            {
                await downloader.EnsureModelAsync(progress);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]  ✓ Model ready. Use 'gauntletci analyze --with-llm' to enable enrichment.[/]");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Download failed: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateStatus()
    {
        var cmd = new Command("status", "Show whether the local LLM model is downloaded and ready");

        cmd.SetHandler(() =>
        {
            var downloader = new ModelDownloader(DefaultModelDir);
            if (downloader.IsModelCached())
            {
                AnsiConsole.MarkupLine($"[green]  ✓ Model cached at {DefaultModelDir}[/]");
                AnsiConsole.MarkupLine("[green]  Run 'gauntletci analyze --with-llm' to enable enrichment.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]  ✗ Model not found at {DefaultModelDir}[/]");
                AnsiConsole.MarkupLine("[yellow]  Run 'gauntletci model download' to download it (~2 GB).[/]");
            }
        });

        return cmd;
    }
}
