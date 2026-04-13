// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Core.Configuration;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// gauntletci ignore GCI0003
/// gauntletci ignore GCI0003 --path src/Generated/**
/// Appends a suppression entry to .gauntletci-ignore.
/// </summary>
public static class IgnoreCommand
{
    public static Command Create()
    {
        var ruleIdArg = new Argument<string>("rule-id", "The rule ID to suppress (e.g. GCI0003)");
        var pathOption = new Option<string?>("--path", "Optional glob path pattern to restrict suppression (e.g. src/Generated/**)");
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");

        var cmd = new Command("ignore", "Add a suppression entry to .gauntletci-ignore")
        {
            ruleIdArg,
            pathOption,
            repoOption,
        };

        cmd.SetHandler((ruleId, path, repo) =>
        {
            var normalizedId = ruleId.ToUpperInvariant();
            IgnoreList.Append(repo.FullName, normalizedId, path);

            var entry = path is not null ? $"{normalizedId}:{path}" : normalizedId;
            AnsiConsole.MarkupLine($"[green][[GauntletCI]] Added '{Markup.Escape(entry)}' to .gauntletci-ignore[/]");
            return Task.CompletedTask;
        }, ruleIdArg, pathOption, repoOption);

        return cmd;
    }
}
