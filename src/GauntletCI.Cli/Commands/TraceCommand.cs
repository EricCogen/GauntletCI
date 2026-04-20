// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text;
using GauntletCI.Cli.IncidentCorrelation;
using GauntletCI.Cli.Presentation;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using Spectre.Console;

namespace GauntletCI.Cli.Commands;

/// <summary>
/// Implements <c>gauntletci trace</c> — post-mortem incident correlation.
/// Gets the diff since a deploy tag, re-runs GauntletCI analysis, optionally fetches
/// PagerDuty/Opsgenie incidents, and outputs a Change Risk Heatmap.
/// </summary>
public static class TraceCommand
{
    public static Command Create()
    {
        var deployTagOption = new Option<string?>(
            "--deploy-tag",
            "Git tag or commit SHA representing the deploy baseline");

        var fromCommitOption = new Option<string?>(
            "--from-commit",
            "Explicit base commit SHA (alternative to --deploy-tag)");

        var sinceOption = new Option<string>(
            "--since",
            () => "24h",
            "Time window for incident fetching, e.g. 24h, 7d, 30m (default: 24h)");

        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (default: current directory)");

        var outputOption = new Option<string>(
            "--output",
            () => "text",
            "Output format: text or json");

        var pdTokenOption = new Option<string?>(
            "--pd-token",
            "PagerDuty API token (or set PAGERDUTY_TOKEN env var)");

        var ogTokenOption = new Option<string?>(
            "--og-token",
            "Opsgenie API key (or set OPSGENIE_TOKEN env var)");

        var postToPdOption = new Option<string?>(
            "--post-to-pd-incident",
            "Post the heatmap as a note on this PagerDuty incident ID");

        var noBannerOption = new Option<bool>(
            "--no-banner",
            "Disable banner");

        var asciiFlag = new Option<bool>(
            "--ascii",
            "ASCII-only output");

        var cmd = new Command("trace", "Post-mortem incident correlation — correlates deploy diff with PagerDuty/Opsgenie incidents")
        {
            deployTagOption,
            fromCommitOption,
            sinceOption,
            repoOption,
            outputOption,
            pdTokenOption,
            ogTokenOption,
            postToPdOption,
            noBannerOption,
            asciiFlag,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var deployTag   = ctx.ParseResult.GetValueForOption(deployTagOption);
            var fromCommit  = ctx.ParseResult.GetValueForOption(fromCommitOption);
            var since       = ctx.ParseResult.GetValueForOption(sinceOption)!;
            var repo        = ctx.ParseResult.GetValueForOption(repoOption)!;
            var output      = ctx.ParseResult.GetValueForOption(outputOption)!;
            var pdToken     = ctx.ParseResult.GetValueForOption(pdTokenOption)
                           ?? Environment.GetEnvironmentVariable("PAGERDUTY_TOKEN");
            var ogToken     = ctx.ParseResult.GetValueForOption(ogTokenOption)
                           ?? Environment.GetEnvironmentVariable("OPSGENIE_TOKEN");
            var postToPd    = ctx.ParseResult.GetValueForOption(postToPdOption);
            var noBanner    = ctx.ParseResult.GetValueForOption(noBannerOption);
            var ascii       = ctx.ParseResult.GetValueForOption(asciiFlag);
            var ct          = ctx.GetCancellationToken();

            var isJson = output.Equals("json", StringComparison.OrdinalIgnoreCase);
            CliBanner.PrintIfEnabled(new BannerContext { NoBanner = noBanner, OutputFormat = output });

            // Validate: need exactly one of --deploy-tag or --from-commit
            var baseRef = deployTag ?? fromCommit;
            if (string.IsNullOrWhiteSpace(baseRef))
            {
                Console.Error.WriteLine("[GauntletCI] Error: specify --deploy-tag <tag> or --from-commit <sha>.");
                ctx.ExitCode = 1;
                return;
            }

            if (deployTag is not null && fromCommit is not null)
            {
                Console.Error.WriteLine("[GauntletCI] Error: --deploy-tag and --from-commit are mutually exclusive.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var now      = DateTimeOffset.UtcNow;
                var sinceDto = IncidentClient.ParseSince(since, now);

                // Step 1: get diff from base ref to HEAD using three-dot range
                var rangeRef = $"{baseRef}...HEAD";
                var diff     = await DiffParser.FromGitAsync(repo.FullName, rangeRef, ct);

                // Step 2: run rule orchestrator
                var config      = ConfigLoader.Load(repo.FullName);
                var ignoreList  = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repo.FullName);
                var result       = await orchestrator.RunAsync(diff, ignoreList: ignoreList);

                // Step 3: fetch incidents (soft-fail)
                var allIncidents = new List<IncidentCorrelation.IncidentSummary>();

                if (!string.IsNullOrWhiteSpace(pdToken))
                {
                    var pdIncidents = await IncidentClient.FetchPagerDutyAsync(pdToken, sinceDto, now, ct);
                    allIncidents.AddRange(pdIncidents);
                    if (!isJson)
                        AnsiConsole.MarkupLine($"[dim]  {(ascii ? "[PD]" : "📟")}  Fetched {pdIncidents.Count} PagerDuty incident(s)[/]");
                }
                else
                {
                    Console.Error.WriteLine("[GauntletCI] No PagerDuty token — skipping PD incident fetch (set --pd-token or PAGERDUTY_TOKEN).");
                }

                if (!string.IsNullOrWhiteSpace(ogToken))
                {
                    var ogAlerts = await IncidentClient.FetchOpsgenieAsync(ogToken, sinceDto, now, ct);
                    allIncidents.AddRange(ogAlerts);
                    if (!isJson)
                        AnsiConsole.MarkupLine($"[dim]  {(ascii ? "[OG]" : "📟")}  Fetched {ogAlerts.Count} Opsgenie alert(s)[/]");
                }
                else
                {
                    Console.Error.WriteLine("[GauntletCI] No Opsgenie token — skipping OG alert fetch (set --og-token or OPSGENIE_TOKEN).");
                }

                // Step 4: correlate
                var correlations = IncidentClient.CorrelateIncidents(result.Findings, allIncidents);

                // Step 5: output
                if (isJson)
                {
                    var json = IncidentClient.BuildHeatmapJson(
                        baseRef, sinceDto, now, result.Findings, correlations, allIncidents);
                    Console.WriteLine(json);
                }
                else
                {
                    PrintHeatmap(baseRef, sinceDto, now, result, correlations, allIncidents, ascii);
                }

                // Step 6: post to PD incident if requested
                if (!string.IsNullOrWhiteSpace(postToPd) && !string.IsNullOrWhiteSpace(pdToken))
                {
                    var textContent = BuildHeatmapText(baseRef, result, correlations, allIncidents);
                    var posted = await IncidentClient.PostPagerDutyNoteAsync(pdToken, postToPd, $"GauntletCI Change Risk Heatmap:\n{textContent}", ct);
                    if (!isJson && posted)
                        AnsiConsole.MarkupLine($"[dim]  {(ascii ? "[OK]" : "✅")}  Posted heatmap note to PagerDuty incident {Markup.Escape(postToPd)}[/]");
                }
                else if (!string.IsNullOrWhiteSpace(postToPd) && string.IsNullOrWhiteSpace(pdToken))
                {
                    Console.Error.WriteLine("[GauntletCI] --post-to-pd-incident requires --pd-token or PAGERDUTY_TOKEN.");
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

    // ── Text rendering ────────────────────────────────────────────────────────

    private static void PrintHeatmap(
        string baseRef,
        DateTimeOffset since,
        DateTimeOffset now,
        GauntletCI.Core.Rules.EvaluationResult result,
        Dictionary<string, List<IncidentCorrelation.IncidentSummary>> correlations,
        List<IncidentCorrelation.IncidentSummary> allIncidents,
        bool ascii)
    {
        var hr = ascii
            ? "======================================================="
            : "═══════════════════════════════════════════════════════";

        AnsiConsole.MarkupLine($"[cyan]{hr}[/]");
        AnsiConsole.MarkupLine("[cyan]  GauntletCI Change Risk Heatmap[/]");
        AnsiConsole.MarkupLine($"[cyan]{hr}[/]");
        AnsiConsole.MarkupLine($"  Base ref : {Markup.Escape(baseRef)}");
        AnsiConsole.MarkupLine($"  Window   : {since:u} {(ascii ? "->" : "→")} {now:u}");
        AnsiConsole.MarkupLine($"  Findings : {result.Findings.Count}");
        AnsiConsole.MarkupLine($"  Incidents: {allIncidents.Count}");
        AnsiConsole.WriteLine();

        var byFile = result.Findings
            .Where(f => !string.IsNullOrWhiteSpace(f.FilePath))
            .GroupBy(f => f.FilePath!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (byFile.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]  No file-level findings.[/]");
            return;
        }

        var table = new Table()
            .Border(ascii ? TableBorder.Ascii : TableBorder.Rounded)
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Severity[/]")
            .AddColumn("[bold]Findings[/]")
            .AddColumn("[bold]Correlated Incidents[/]");

        foreach (var grp in byFile.OrderByDescending(g => g.Max(f => f.Severity)))
        {
            var filePath = grp.Key;
            var maxSev   = grp.Max(f => f.Severity);
            var sevColor = maxSev switch
            {
                RuleSeverity.Block => "red",
                RuleSeverity.Warn  => "yellow",
                _                  => "grey",
            };

            var findingsSummary = string.Join(", ", grp.Select(f => f.RuleId).Distinct());

            correlations.TryGetValue(filePath, out var correlated);
            var incidentText = (correlated is null || correlated.Count == 0)
                ? "[grey]none[/]"
                : string.Join(", ", correlated.Select(i => Markup.Escape($"{i.Source}:{i.Id}")));

            table.AddRow(
                Markup.Escape(filePath),
                $"[{sevColor}]{maxSev}[/]",
                Markup.Escape(findingsSummary),
                incidentText);
        }

        AnsiConsole.Write(table);
    }

    private static string BuildHeatmapText(
        string baseRef,
        GauntletCI.Core.Rules.EvaluationResult result,
        Dictionary<string, List<IncidentCorrelation.IncidentSummary>> correlations,
        List<IncidentCorrelation.IncidentSummary> allIncidents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Base ref: {baseRef}");
        sb.AppendLine($"Findings: {result.Findings.Count} | Incidents: {allIncidents.Count}");
        sb.AppendLine();

        var byFile = result.Findings
            .Where(f => !string.IsNullOrWhiteSpace(f.FilePath))
            .GroupBy(f => f.FilePath!, StringComparer.OrdinalIgnoreCase);

        foreach (var grp in byFile.OrderByDescending(g => g.Max(f => f.Severity)))
        {
            var filePath = grp.Key;
            var maxSev   = grp.Max(f => f.Severity);
            correlations.TryGetValue(filePath, out var correlated);

            sb.AppendLine($"  {filePath} [{maxSev}]");
            foreach (var f in grp)
                sb.AppendLine($"    - [{f.RuleId}] {f.Summary}");

            if (correlated is { Count: > 0 })
                sb.AppendLine($"    Incidents: {string.Join(", ", correlated.Select(i => $"{i.Source}:{i.Id} \"{i.Title}\""))}");
        }

        return sb.ToString();
    }
}
