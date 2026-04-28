// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Baseline;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Rules;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// Implements <c>gauntletci doctor</c>: a self-diagnostic command that validates
/// the local environment: config files, rule status, Ollama connectivity, and baseline.
/// </summary>
public static class DoctorCommand
{
    public static Command Create()
    {
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");

        var cmd = new Command("doctor",
            "Check GauntletCI environment: config, rules, Ollama connectivity, and baseline status")
        {
            repoOption,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var repo = ctx.ParseResult.GetValueForOption(repoOption)!;
            var ct   = ctx.GetCancellationToken();

            var repoRoot = FindGitRoot(repo.FullName);

            AnsiConsole.MarkupLine("[bold cyan]GauntletCI Doctor[/]");
            AnsiConsole.MarkupLine("[dim]─────────────────────────────────────────────────────[/]");
            AnsiConsole.WriteLine();

            // ── 1. Config files ─────────────────────────────────────────────
            AnsiConsole.MarkupLine("[bold]Config files[/]");

            var homeConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gauntletci.json");

            var repoConfigPath = repoRoot is not null
                ? Path.Combine(repoRoot, ".gauntletci.json")
                : null;

            if (File.Exists(homeConfigPath))
                AnsiConsole.MarkupLine($"[green]  ✓[/] Home config    : {Markup.Escape(homeConfigPath)}");
            else
                AnsiConsole.MarkupLine($"[dim]  -[/] Home config    : {Markup.Escape(homeConfigPath)} [dim](not found)[/]");

            if (repoConfigPath is not null && File.Exists(repoConfigPath))
                AnsiConsole.MarkupLine($"[green]  ✓[/] Repo config    : {Markup.Escape(repoConfigPath)}");
            else if (repoConfigPath is not null)
                AnsiConsole.MarkupLine($"[dim]  -[/] Repo config    : {Markup.Escape(repoConfigPath)} [dim](not found: using defaults)[/]");
            else
                AnsiConsole.MarkupLine("[yellow]  ![/] Repo config    : not inside a Git repository");

            AnsiConsole.WriteLine();

            // ── 2. Effective config ──────────────────────────────────────────
            AnsiConsole.MarkupLine("[bold]Effective config[/]");
            var effectiveRoot = repoRoot ?? repo.FullName;
            var config = ConfigLoader.Load(effectiveRoot);

            AnsiConsole.MarkupLine($"  ExitOn      : {config.ExitOn}");
            AnsiConsole.MarkupLine($"  LLM model   : {config.Llm?.CiModel ?? "(none: local only)"}");
            AnsiConsole.MarkupLine($"  Ollama URL  : {config.Llm?.EmbeddingOllamaUrl ?? "http://localhost:11434"}");
            AnsiConsole.MarkupLine($"  Ollama model: {config.Llm?.Model ?? LlmDefaults.OllamaModel}");
            AnsiConsole.MarkupLine($"  EP policy   : {(config.Experimental.EngineeringPolicy.Enabled ? "[green]enabled[/]" : "[dim]disabled[/]")}");

            AnsiConsole.WriteLine();

            // ── 3. Rules ─────────────────────────────────────────────────────
            AnsiConsole.MarkupLine("[bold]Rules[/]");
            var allIds   = RuleOrchestrator.GetAllRuleIds();
            var disabled = allIds
                .Where(id => config.Rules.TryGetValue(id, out var rc) && !rc.Enabled)
                .ToList();
            var enabled  = allIds.Count - disabled.Count;

            AnsiConsole.MarkupLine($"  Total       : {allIds.Count}");
            AnsiConsole.MarkupLine($"  Enabled     : [green]{enabled}[/]");

            if (disabled.Count > 0)
            {
                AnsiConsole.MarkupLine($"  Disabled    : [yellow]{disabled.Count}[/] ({string.Join(", ", disabled)})");
            }
            else
            {
                AnsiConsole.MarkupLine("  Disabled    : [dim]none[/]");
            }

            AnsiConsole.WriteLine();

            // ── 4. Ollama connectivity ───────────────────────────────────────
            AnsiConsole.MarkupLine("[bold]Ollama connectivity[/]");
            var ollamaUrl = config.Llm?.EmbeddingOllamaUrl ?? "http://localhost:11434";
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await http.GetAsync(ollamaUrl, ct);
                if (response.IsSuccessStatusCode)
                    AnsiConsole.MarkupLine($"[green]  ✓[/] Reachable at {Markup.Escape(ollamaUrl)}");
                else
                    AnsiConsole.MarkupLine($"[yellow]  ![/] Responded with {(int)response.StatusCode} at {Markup.Escape(ollamaUrl)}");
            }
            catch
            {
                AnsiConsole.MarkupLine($"[yellow]  ![/] Not reachable at {Markup.Escape(ollamaUrl)} [dim](start Ollama or update EmbeddingOllamaUrl in config)[/]");
            }

            AnsiConsole.WriteLine();

            // ── 5. Baseline ──────────────────────────────────────────────────
            AnsiConsole.MarkupLine("[bold]Baseline[/]");
            try
            {
                var baseline = BaselineStore.Load(effectiveRoot);
                if (baseline is not null)
                {
                    AnsiConsole.MarkupLine($"[green]  ✓[/] Active: {baseline.Fingerprints.Count} fingerprint(s), created {baseline.CreatedAt:u}");
                    if (baseline.Commit is not null)
                        AnsiConsole.MarkupLine($"  [dim]  Commit: {Markup.Escape(baseline.Commit)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim] ,  No baseline found (run 'gauntletci baseline create --staged' to create one)[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]  ![/] Baseline file is invalid or unreadable: {Markup.Escape(ex.Message)}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]─────────────────────────────────────────────────────[/]");
            AnsiConsole.MarkupLine("[dim]Run 'gauntletci init' to create a default config.[/]");

            ctx.ExitCode = 0;
        });

        return cmd;
    }

    private static string? FindGitRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}
