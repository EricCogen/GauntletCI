// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Baseline;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.StaticAnalysis;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// Implements <c>gauntletci baseline create|clear|show</c>.
/// Baselines allow teams to snapshot the current set of findings and suppress them in future
/// runs so that <c>gauntletci analyze</c> only surfaces net-new issues.
/// </summary>
public static class BaselineCommand
{
    public static Command Create()
    {
        var cmd = new Command("baseline", "Manage analysis baselines to suppress known findings");
        cmd.AddCommand(BuildCreate());
        cmd.AddCommand(BuildClear());
        cmd.AddCommand(BuildShow());
        return cmd;
    }

    // ── baseline create ──────────────────────────────────────────────────────

    private static Command BuildCreate()
    {
        var diffOption     = new Option<FileInfo?>("--diff",        "Path to a .diff file");
        var commitOption   = new Option<string?>("--commit",        "Commit SHA to analyse");
        var stagedFlag     = new Option<bool>("--staged",           "Analyse staged changes (git diff --cached)");
        var unstagedFlag   = new Option<bool>("--unstaged",         "Analyse unstaged changes (git diff)");
        var allChangesFlag = new Option<bool>("--all-changes",      "Analyse all local changes: staged + unstaged");
        var repoOption     = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");

        var create = new Command("create",
            "Run analysis on the current diff and record all findings as the new baseline")
        {
            diffOption, commitOption, stagedFlag, unstagedFlag, allChangesFlag, repoOption,
        };

        create.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var diffFile   = ctx.ParseResult.GetValueForOption(diffOption);
            var commit     = ctx.ParseResult.GetValueForOption(commitOption);
            var staged     = ctx.ParseResult.GetValueForOption(stagedFlag);
            var unstaged   = ctx.ParseResult.GetValueForOption(unstagedFlag);
            var allChanges = ctx.ParseResult.GetValueForOption(allChangesFlag);
            var repo       = ctx.ParseResult.GetValueForOption(repoOption)!;
            var ct         = ctx.GetCancellationToken();

            var sourceCount = (diffFile   is not null ? 1 : 0)
                            + (commit     is not null ? 1 : 0)
                            + (staged     ? 1 : 0)
                            + (unstaged   ? 1 : 0)
                            + (allChanges ? 1 : 0);

            if (sourceCount > 1)
            {
                Console.Error.WriteLine("[GauntletCI] Error: specify exactly one of --diff, --commit, --staged, --unstaged, --all-changes.");
                ctx.ExitCode = 1;
                return;
            }

            if (sourceCount == 0 && !Console.IsInputRedirected)
            {
                Console.Error.WriteLine("[GauntletCI] Error: no diff source specified. Use --staged, --all-changes, --commit <sha>, --diff <file>, or pipe a diff to stdin.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var diff = diffFile is not null
                    ? DiffParser.FromFile(diffFile.FullName)
                    : commit is not null
                        ? await DiffParser.FromGitAsync(repo.FullName, commit, ct)
                        : staged
                            ? await DiffParser.FromStagedAsync(repo.FullName, ct)
                            : unstaged
                                ? await DiffParser.FromUnstagedAsync(repo.FullName, ct)
                                : allChanges
                                    ? await DiffParser.FromAllChangesAsync(repo.FullName, ct)
                                    : DiffParser.Parse(await Console.In.ReadToEndAsync(ct));

                var config       = ConfigLoader.Load(repo.FullName);
                var ignoreList   = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repo.FullName);
                var staticAnalysis = await StaticAnalysisRunner.RunAsync(diff, repo.FullName, ct);
                var result       = await orchestrator.RunAsync(diff, staticAnalysis, ignoreList: ignoreList);

                var fingerprints = result.Findings.Select(BaselineStore.ComputeFingerprint).ToList();
                BaselineStore.Save(repo.FullName, fingerprints, commit);

                AnsiConsole.MarkupLine($"[green]Baseline created:[/] {fingerprints.Count} finding(s) recorded.");
                AnsiConsole.MarkupLine($"[dim]  {Markup.Escape(BaselineStore.GetPath(repo.FullName))}[/]");

                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Error: {ex.Message}");
                ctx.ExitCode = 2;
            }
        });

        return create;
    }

    // ── baseline clear ───────────────────────────────────────────────────────

    private static Command BuildClear()
    {
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");

        var clear = new Command("clear", "Delete the current baseline file") { repoOption };

        clear.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var repo = ctx.ParseResult.GetValueForOption(repoOption)!;

            if (BaselineStore.Clear(repo.FullName))
                AnsiConsole.MarkupLine("[green]Baseline cleared.[/]");
            else
                AnsiConsole.MarkupLine("[yellow]No baseline found — nothing to clear.[/]");

            ctx.ExitCode = 0;
        });

        return clear;
    }

    // ── baseline show ────────────────────────────────────────────────────────

    private static Command BuildShow()
    {
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");

        var show = new Command("show", "Display the current baseline metadata and fingerprints") { repoOption };

        show.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var repo = ctx.ParseResult.GetValueForOption(repoOption)!;

            BaselineFile? baseline;
            try
            {
                baseline = BaselineStore.Load(repo.FullName);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]  ![/] Baseline file is invalid or unreadable: {Markup.Escape(ex.Message)}");
                ctx.ExitCode = 1;
                return;
            }

            if (baseline is null)
            {
                AnsiConsole.MarkupLine("[yellow]No baseline found.[/]");
                AnsiConsole.MarkupLine($"[dim]  Run 'gauntletci baseline create --staged' to create one.[/]");
                ctx.ExitCode = 0;
                return;
            }

            AnsiConsole.MarkupLine($"[bold cyan]Baseline[/] — {baseline.Fingerprints.Count} fingerprint(s)");
            AnsiConsole.MarkupLine($"[dim]  Created : {baseline.CreatedAt:u}[/]");
            if (baseline.Commit is not null)
                AnsiConsole.MarkupLine($"[dim]  Commit  : {Markup.Escape(baseline.Commit)}[/]");
            AnsiConsole.MarkupLine($"[dim]  Path    : {Markup.Escape(BaselineStore.GetPath(repo.FullName))}[/]");
            AnsiConsole.WriteLine();

            foreach (var fp in baseline.Fingerprints.OrderBy(s => s))
                Console.WriteLine($"  {fp}");

            ctx.ExitCode = 0;
        });

        return show;
    }
}
