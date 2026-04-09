// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Cli.Output;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Llm;

namespace GauntletCI.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create()
    {
        var diffOption = new Option<FileInfo?>("--diff", "Path to a .diff file");
        var commitOption = new Option<string?>("--commit", "Commit SHA to analyse");
        var stagedFlag = new Option<bool>("--staged", "Analyse staged changes (git diff --cached)");
        var unstagedFlag = new Option<bool>("--unstaged", "Analyse unstaged changes (git diff)");
        var allChangesFlag = new Option<bool>("--all-changes", "Analyse all local changes: staged + unstaged (git diff HEAD)");
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");
        var outputOption = new Option<string>(
            "--output",
            () => "text",
            "Output format: text or json");
        var noLlmFlag = new Option<bool>("--no-llm", "Disable LLM enrichment");
        var asciiFlag = new Option<bool>("--ascii", "Use ASCII-only output (for terminals without Unicode support)");

        var cmd = new Command("analyze", "Analyse a git diff for pre-commit risks")
        {
            diffOption,
            commitOption,
            stagedFlag,
            unstagedFlag,
            allChangesFlag,
            repoOption,
            outputOption,
            noLlmFlag,
            asciiFlag,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var diffFile   = ctx.ParseResult.GetValueForOption(diffOption);
            var commit     = ctx.ParseResult.GetValueForOption(commitOption);
            var staged     = ctx.ParseResult.GetValueForOption(stagedFlag);
            var unstaged   = ctx.ParseResult.GetValueForOption(unstagedFlag);
            var allChanges = ctx.ParseResult.GetValueForOption(allChangesFlag);
            var repo       = ctx.ParseResult.GetValueForOption(repoOption)!;
            var output     = ctx.ParseResult.GetValueForOption(outputOption)!;
            var noLlm      = ctx.ParseResult.GetValueForOption(noLlmFlag);
            var ascii      = ctx.ParseResult.GetValueForOption(asciiFlag);

            try
            {
                var diff = diffFile is not null
                    ? DiffParser.FromFile(diffFile.FullName)
                    : commit is not null
                        ? await DiffParser.FromGitAsync(repo.FullName, commit)
                        : staged
                            ? await DiffParser.FromStagedAsync(repo.FullName)
                            : unstaged
                                ? await DiffParser.FromUnstagedAsync(repo.FullName)
                                : allChanges
                                    ? await DiffParser.FromAllChangesAsync(repo.FullName)
                                    : DiffParser.Parse(await Console.In.ReadToEndAsync());

                var orchestrator = RuleOrchestrator.CreateDefault();
                var result = await orchestrator.RunAsync(diff);

                ILlmEngine llm = noLlm ? new NullLlmEngine() : new NullLlmEngine();

                if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);
                }
                else
                {
                    ConsoleReporter.Report(result, ascii);
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
