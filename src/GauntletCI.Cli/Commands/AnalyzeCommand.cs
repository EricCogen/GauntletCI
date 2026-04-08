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
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");
        var outputOption = new Option<string>(
            "--output",
            () => "text",
            "Output format: text or json");
        var noLlmFlag = new Option<bool>("--no-llm", "Disable LLM enrichment");

        var cmd = new Command("analyze", "Analyse a git diff for pre-commit risks")
        {
            diffOption,
            commitOption,
            repoOption,
            outputOption,
            noLlmFlag,
        };

        cmd.SetHandler(async (diffFile, commit, repo, output, noLlm) =>
        {
            try
            {
                var diff = diffFile is not null
                    ? DiffParser.FromFile(diffFile.FullName)
                    : commit is not null
                        ? await DiffParser.FromGitAsync(repo.FullName, commit)
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
                    ConsoleReporter.Report(result);
                }

                Environment.Exit(result.HasFindings ? 1 : 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Error: {ex.Message}");
                Environment.Exit(2);
            }
        },
        diffOption, commitOption, repoOption, outputOption, noLlmFlag);

        return cmd;
    }
}
