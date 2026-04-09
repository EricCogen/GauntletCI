// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Llm;

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
            var progress = new Progress<string>(msg => Console.WriteLine(msg));

            try
            {
                await downloader.EnsureModelAsync(progress);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Model ready. Use 'gauntletci analyze --with-llm' to enable enrichment.");
                Console.ResetColor();
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ Model cached at {DefaultModelDir}");
                Console.WriteLine("  Run 'gauntletci analyze --with-llm' to enable enrichment.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ✗ Model not found at {DefaultModelDir}");
                Console.WriteLine("  Run 'gauntletci model download' to download it (~2 GB).");
            }
            Console.ResetColor();
        });

        return cmd;
    }
}
