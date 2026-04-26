// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using GauntletCI.Cli.Output;
using GauntletCI.Cli.Presentation;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

public static class PostmortemCommand
{
    public static Command Create()
    {
        var commitOption = new Option<string>("--commit", "Commit SHA to analyse (required)")
            { IsRequired = true };
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");
        var outputOption = new Option<string>("--output", () => "text", "Output format: text or json");
        var noBannerOption = new Option<bool>("--no-banner", "Disable banner");
        var asciiFlag = new Option<bool>("--ascii", "ASCII-only output");

        var cmd = new Command("postmortem", "Analyse a past commit — see what GauntletCI would have caught")
        {
            commitOption,
            repoOption,
            outputOption,
            noBannerOption,
            asciiFlag,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var commit   = ctx.ParseResult.GetValueForOption(commitOption)!;
            var repo     = ctx.ParseResult.GetValueForOption(repoOption)!;
            var output   = ctx.ParseResult.GetValueForOption(outputOption)!;
            var noBanner = ctx.ParseResult.GetValueForOption(noBannerOption);
            var ascii    = ctx.ParseResult.GetValueForOption(asciiFlag);

            CliBanner.PrintIfEnabled(new BannerContext { NoBanner = noBanner, OutputFormat = output });

            try
            {
                var diff = await DiffParser.FromGitAsync(repo.FullName, commit);
                var config = ConfigLoader.Load(repo.FullName);
                var ignoreList = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config);
                var sw = Stopwatch.StartNew();
                var result = await orchestrator.RunAsync(diff, ignoreList: ignoreList);
                sw.Stop();

                if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    var shortSha = commit.Length >= 8 ? commit[..8] : commit;
                    AnsiConsole.MarkupLine($"[dim]  ⏪  Postmortem — commit {Markup.Escape(shortSha)}[/]");
                    AnsiConsole.MarkupLine("[dim]     These findings would have been caught at pre-commit time.[/]");
                    AnsiConsole.WriteLine();
                    ConsoleReporter.Report(result, ascii, elapsed: sw.Elapsed);
                }

                ctx.ExitCode = result.HasFindings ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Error: {ex.Message}");
                ctx.ExitCode = 2;
            }
        });

        return cmd;
    }
}
