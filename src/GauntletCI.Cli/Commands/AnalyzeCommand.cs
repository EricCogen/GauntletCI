// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Cli.Output;
using GauntletCI.Cli.Presentation;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
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
        var noLlmFlag = new Option<bool>("--no-llm", "Disable LLM enrichment (deprecated — LLM is now opt-in via --with-llm)") { IsHidden = true };
        var withLlmFlag = new Option<bool>("--with-llm", "Enable LLM enrichment of High-confidence findings (requires 'gauntletci model download', adds latency)");
        var asciiFlag = new Option<bool>("--ascii", "Use ASCII-only output (for terminals without Unicode support)");
        var noBannerOption = new Option<bool>("--no-banner", "Disable ASCII banner");
        var githubAnnotationsFlag = new Option<bool>("--github-annotations", "Emit GitHub Actions workflow commands for inline PR annotations");

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
            withLlmFlag,
            asciiFlag,
            noBannerOption,
            githubAnnotationsFlag,
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
            var withLlm    = ctx.ParseResult.GetValueForOption(withLlmFlag);
            var ascii      = ctx.ParseResult.GetValueForOption(asciiFlag);
            var noBanner   = ctx.ParseResult.GetValueForOption(noBannerOption);
            var ghAnnotate = ctx.ParseResult.GetValueForOption(githubAnnotationsFlag);

            CliBanner.PrintIfEnabled(new BannerContext
            {
                NoBanner = noBanner,
                OutputFormat = output ?? "text",
            });

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

                var config = ConfigLoader.Load(repo.FullName);
                var ignoreList = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config);
                var result = await orchestrator.RunAsync(diff, ignoreList: ignoreList);

                using ILlmEngine llm = (withLlm && !noLlm) ? new LocalLlmEngine() : new NullLlmEngine();
                if (llm.IsAvailable)
                {
                    foreach (var finding in result.Findings.Where(f => f.Confidence == Confidence.High))
                        finding.LlmExplanation = await llm.EnrichFindingAsync(finding);
                }

                if ((output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);
                }
                else
                {
                    ConsoleReporter.Report(result, ascii);
                }

                if (ghAnnotate)
                    GitHubAnnotationWriter.Write(result);

                await TelemetryCollector.CollectAsync(result, diff, repo.FullName);

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
