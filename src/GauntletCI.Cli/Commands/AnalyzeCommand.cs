// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Cli.Analysis;
using GauntletCI.Cli.Audit;
using GauntletCI.Cli.LlmDaemon;
using GauntletCI.Cli.Output;
using GauntletCI.Cli.Presentation;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.StaticAnalysis;
using GauntletCI.Llm;
using Spectre.Console;

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
        var withExpertCtxFlag = new Option<bool>("--with-expert-context",
            "Attach matching expert facts from the local vector store to findings (requires 'gauntletci llm seed')");
        var verboseFlag = new Option<bool>("--verbose", "Show Info-severity findings in addition to Warn and Block");
        var severityOption = new Option<string>(
            "--severity",
            () => "warn",
            "Minimum severity to display: info, warn, block");

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
            withExpertCtxFlag,
            verboseFlag,
            severityOption,
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
            var withExpertCtx = ctx.ParseResult.GetValueForOption(withExpertCtxFlag);
            var verbose    = ctx.ParseResult.GetValueForOption(verboseFlag);
            var severityStr = ctx.ParseResult.GetValueForOption(severityOption)!;

            // Enforce single diff source
            int sourceCount = (diffFile is not null ? 1 : 0)
                            + (commit    is not null ? 1 : 0)
                            + (staged      ? 1 : 0)
                            + (unstaged    ? 1 : 0)
                            + (allChanges  ? 1 : 0);
            if (sourceCount > 1)
            {
                Console.Error.WriteLine("[GauntletCI] Error: multiple diff sources specified. Use exactly one of: --diff, --commit, --staged, --unstaged, --all-changes.");
                ctx.ExitCode = 1;
                return;
            }

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
                var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repo.FullName);

                // Run static analysis on changed C# files (null when no repo path or no .cs changes)
                var repoPath = diffFile is null ? repo.FullName : null;
                var staticAnalysis = await StaticAnalysisRunner.RunAsync(diff, repoPath);

                var result = await orchestrator.RunAsync(diff, staticAnalysis, ignoreList: ignoreList);

                using ILlmEngine llm = await LlmEngineSelector.ResolveAsync(config, withLlm && !noLlm);

                var isJsonOutput = (output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase);
                var showSpinner  = llm.IsAvailable && !isJsonOutput && !Console.IsOutputRedirected;

                async Task RunLlmStepsAsync(Action<string>? setStatus = null)
                {
                    setStatus ??= _ => { };

                    if (llm.IsAvailable)
                    {
                        var highFindings = result.Findings.Where(f => f.Confidence == Confidence.High).ToList();
                        foreach (var finding in highFindings)
                        {
                            setStatus($"Annotating [{finding.RuleId}]...");
                            finding.LlmExplanation = await llm.EnrichFindingAsync(finding);
                        }
                    }

                    if (withExpertCtx)
                    {
                        var vectorDbPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".gauntletci", "expert-embeddings.db");

                        if (File.Exists(vectorDbPath))
                        {
                            setStatus("Adding context...");
                            var ct = ctx.GetCancellationToken();
                            using var store    = new GauntletCI.Llm.Embeddings.VectorStore(vectorDbPath);
                            using var embedEng = new GauntletCI.Llm.Embeddings.OllamaEmbeddingEngine();
                            var adjudicator    = new GauntletCI.Llm.Embeddings.LlmAdjudicator(embedEng, store);
                            await adjudicator.AdjudicateAsync(result.Findings, ct);
                        }
                    }

                    // Engineering policy evaluation (experimental -- config-driven, LLM-required)
                    if (config.Experimental.EngineeringPolicy.Enabled && llm.IsAvailable)
                    {
                        setStatus("Checking standards...");
                        var policyPath  = Path.Combine(repo.FullName, config.Experimental.EngineeringPolicy.Path);
                        var licenseKey  = config.Llm is not null
                            ? Environment.GetEnvironmentVariable(config.Llm.LicenseKeyEnv)
                            : null;
                        var isLicensed  = !string.IsNullOrWhiteSpace(licenseKey);
                        var policyFindings = await EngineeringPolicyEvaluator.EvaluateAsync(
                            diff, policyPath, llm, isLicensed,
                            config.Experimental.EngineeringPolicy.MaxDiffChars,
                            ctx.GetCancellationToken());
                        result.Findings.AddRange(policyFindings);
                    }
                }

                if (showSpinner)
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan dim"))
                        .StartAsync("Thinking...", async sc =>
                            await RunLlmStepsAsync(s => sc.Status = s));
                else
                    await RunLlmStepsAsync();

                if ((output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);
                }
                else
                {
                    var minSeverity = verbose
                        ? GauntletCI.Core.Model.RuleSeverity.Info
                        : ParseMinSeverity(severityStr);
                    ConsoleReporter.Report(result, ascii, minSeverity);
                }

                if (ghAnnotate)
                    GitHubAnnotationWriter.Write(result);

                await TelemetryCollector.CollectAsync(result, diff, repo.FullName);

                // Append full-detail entry to local audit log
                var diffSource = diffFile is not null ? "file"
                    : commit is not null ? "commit"
                    : staged ? "staged"
                    : unstaged ? "unstaged"
                    : allChanges ? "all-changes"
                    : "stdin";

                await AuditLog.AppendAsync(new AuditLogEntry
                {
                    RepoPath       = repo.FullName,
                    CommitSha      = result.CommitSha,
                    DiffSource     = diffSource,
                    FilesChanged   = result.FileStatistics.TotalFiles,
                    FilesEligible  = result.FileStatistics.EligibleFiles,
                    RulesEvaluated = result.RulesEvaluated,
                    FindingCount   = result.Findings.Count,
                    Findings       = [.. result.Findings.Select(f => new AuditFinding
                    {
                        RuleId     = f.RuleId,
                        RuleName   = f.RuleName,
                        Summary    = f.Summary,
                        Confidence = f.Confidence.ToString(),
                        FilePath   = f.FilePath,
                        Line       = f.Line,
                    })],
                });

                ctx.ExitCode = result.ShouldBlock(config.ExitOn) ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Error: {ex.Message}");
                ctx.ExitCode = 2;
            }
        });

        return cmd;
    }

    private static GauntletCI.Core.Model.RuleSeverity ParseMinSeverity(string s) =>
        s.ToLowerInvariant() switch
        {
            "block" => GauntletCI.Core.Model.RuleSeverity.Block,
            "info"  => GauntletCI.Core.Model.RuleSeverity.Info,
            _       => GauntletCI.Core.Model.RuleSeverity.Warn,
        };
}
