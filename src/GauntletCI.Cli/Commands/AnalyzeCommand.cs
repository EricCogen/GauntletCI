// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using GauntletCI.Cli.Analysis;
using GauntletCI.Cli.Audit;
using GauntletCI.Cli.Baseline;
using GauntletCI.Cli.Enrichment;
using GauntletCI.Cli.Licensing;
using GauntletCI.Cli.LlmDaemon;
using GauntletCI.Cli.Output;
using GauntletCI.Cli.Presentation;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Cli.TicketProviders;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Licensing;
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
        var codebaseOption = new Option<DirectoryInfo?>("--codebase", "Full codebase scan: analyse all C# files in directory (e.g., ./src). Treats all code as new for rule evaluation.");
        var repoOption = new Option<DirectoryInfo>(
            "--repo",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Repository root (defaults to current directory)");
        var outputOption = new Option<string>(
            "--output",
            () => "text",
            "Output format: text, json, html, or sarif. Can also be a filename (e.g., report.html) to infer format and save to file");
        var noLlmFlag = new Option<bool>("--no-llm", "Disable LLM enrichment (deprecated: LLM is now opt-in via --with-llm)") { IsHidden = true };
        var withLlmFlag = new Option<bool>("--with-llm", "Enable LLM enrichment of High-confidence findings (requires 'gauntletci model download', adds latency)");
        var asciiFlag = new Option<bool>("--ascii", "Use ASCII-only output (for terminals without Unicode support)");
        var noBannerOption = new Option<bool>("--no-banner", "Disable ASCII banner");
        var githubAnnotationsFlag = new Option<bool>("--github-annotations", "Emit GitHub Actions workflow commands for inline PR annotations");
        var githubPrCommentsFlag = new Option<bool>("--github-pr-comments",
            "Post findings as a GitHub PR review with inline comments (requires pull-requests: write permission)");
        var withExpertCtxFlag = new Option<bool>("--with-expert-context",
            "Attach matching expert facts from the local vector store to findings (requires 'gauntletci llm seed')");
        var verboseFlag = new Option<bool>("--verbose", "Show Info-severity findings in addition to Warn and Block");
        var severityOption = new Option<string>(
            "--severity",
            () => "warn",
            "Minimum severity to display: info, warn, block");
        var sensitivityOption = new Option<string>(
            "--sensitivity",
            () => "balanced",
            "Confidence-based filter threshold: strict (Block+High/Medium only), balanced (default: Block-all + Warn+High/Medium), permissive (all Block and Warn)");
        var noBaselineFlag = new Option<bool>("--no-baseline", "Ignore the baseline file and show all findings");
        var showContextOption = new Option<int>("--show-context", () => 0, "Include N surrounding diff lines around each finding evidence");
        var prCommentSuggestFlag = new Option<bool>("--pr-comment-suggest", "Print PR review comment body to stdout; when used without --github-pr-comments this avoids posting to the GitHub API");
        var notifySlackOption = new Option<string?>("--notify-slack", "Slack Incoming Webhook URL (or set GAUNTLETCI_SLACK_WEBHOOK env var). Posts Block findings to Slack.");
        var notifyTeamsOption = new Option<string?>("--notify-teams", "Teams Incoming Webhook URL (or set GAUNTLETCI_TEAMS_WEBHOOK env var). Posts Block findings to Teams.");
        var withCoverageFlag = new Option<bool>("--with-coverage", "Correlate findings with Codecov coverage data (requires CODECOV_TOKEN env var)");
        var githubChecksFlag = new Option<bool>("--github-checks", "Post findings as a GitHub Checks API check run with annotations (requires checks: write permission)");
        var withTicketCtxFlag = new Option<bool>("--with-ticket-context",
            "Fetch ticket info from Jira/Linear/GitHub Issues and attach to findings (reads branch name and JIRA_*, LINEAR_API_KEY, or GITHUB_TOKEN)");

        var cmd = new Command("analyze", "Analyse a git diff for pre-commit risks")
        {
            diffOption,
            commitOption,
            stagedFlag,
            unstagedFlag,
            allChangesFlag,
            codebaseOption,
            repoOption,
            outputOption,
            noLlmFlag,
            withLlmFlag,
            asciiFlag,
            noBannerOption,
            githubAnnotationsFlag,
            githubPrCommentsFlag,
            withExpertCtxFlag,
            verboseFlag,
            severityOption,
            sensitivityOption,
            noBaselineFlag,
            showContextOption,
            prCommentSuggestFlag,
            notifySlackOption,
            notifyTeamsOption,
            withCoverageFlag,
            githubChecksFlag,
            withTicketCtxFlag,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var diffFile = ctx.ParseResult.GetValueForOption(diffOption);
            var commit = ctx.ParseResult.GetValueForOption(commitOption);
            var staged = ctx.ParseResult.GetValueForOption(stagedFlag);
            var unstaged = ctx.ParseResult.GetValueForOption(unstagedFlag);
            var allChanges = ctx.ParseResult.GetValueForOption(allChangesFlag);
            var codebase = ctx.ParseResult.GetValueForOption(codebaseOption);
            var repo = ctx.ParseResult.GetValueForOption(repoOption)!;
            var output = ctx.ParseResult.GetValueForOption(outputOption)!;
            var noLlm = ctx.ParseResult.GetValueForOption(noLlmFlag);
            var withLlm = ctx.ParseResult.GetValueForOption(withLlmFlag);
            var ascii = ctx.ParseResult.GetValueForOption(asciiFlag);
            var noBanner = ctx.ParseResult.GetValueForOption(noBannerOption);
            var ghAnnotate = ctx.ParseResult.GetValueForOption(githubAnnotationsFlag);
            var ghPrComments = ctx.ParseResult.GetValueForOption(githubPrCommentsFlag);
            var withExpertCtx = ctx.ParseResult.GetValueForOption(withExpertCtxFlag);
            var verbose = ctx.ParseResult.GetValueForOption(verboseFlag);
            var severityStr = ctx.ParseResult.GetValueForOption(severityOption)!;
            var sensitivityStr = ctx.ParseResult.GetValueForOption(sensitivityOption)!;
            var noBaseline = ctx.ParseResult.GetValueForOption(noBaselineFlag);
            var showContext = ctx.ParseResult.GetValueForOption(showContextOption);
            var prCommentSuggest = ctx.ParseResult.GetValueForOption(prCommentSuggestFlag);
            var withCoverage = ctx.ParseResult.GetValueForOption(withCoverageFlag);
            var githubChecks = ctx.ParseResult.GetValueForOption(githubChecksFlag);
            var withTicketCtx = ctx.ParseResult.GetValueForOption(withTicketCtxFlag);
            var notifySlack = ctx.ParseResult.GetValueForOption(notifySlackOption);
            var notifyTeams = ctx.ParseResult.GetValueForOption(notifyTeamsOption);
            var ct = ctx.GetCancellationToken();

            // Infer output format from filename if needed (e.g., --output report.html -> html format, save to file)
            string? outputFile = null;
            if (!string.IsNullOrEmpty(output) && output.Contains('.') &&
                (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                 output.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                 output.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase)))
            {
                outputFile = output;
                output = Path.GetExtension(output).TrimStart('.').ToLowerInvariant();
            }

            // Default to staged changes when no explicit source is provided and stdin is not redirected
            if (diffFile is null && commit is null && !staged && !unstaged && !allChanges &&
                codebase is null && !Console.IsInputRedirected)
            {
                staged = true;
            }

            // Enforce single diff source
            int sourceCount = (diffFile is not null ? 1 : 0)
                            + (commit is not null ? 1 : 0)
                            + (staged ? 1 : 0)
                            + (unstaged ? 1 : 0)
                            + (allChanges ? 1 : 0)
                            + (codebase is not null ? 1 : 0);
            if (sourceCount > 1)
            {
                Console.Error.WriteLine("[GauntletCI] Error: multiple diff sources specified. Use exactly one of: --diff, --commit, --staged, --unstaged, --all-changes, --codebase.");
                ctx.ExitCode = 1;
                return;
            }

            if (prCommentSuggest && ("json".Equals(output, StringComparison.OrdinalIgnoreCase)
                || "html".Equals(output, StringComparison.OrdinalIgnoreCase)
                || "sarif".Equals(output, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("[GauntletCI] Error: --pr-comment-suggest cannot be combined with --output json/html/sarif (produces invalid output). Use --output text or omit --output.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var config = ConfigLoader.Load(repo.FullName);
                config.Ci ??= new();
                config.Output ??= new();
                config.Notifications ??= new();
                config.TicketProvider ??= new();

                // Show metadata before codebase scan so user understands the scope
                if (codebase is not null)
                {
                    try
                    {
                        var metadata = await CodebaseAnalyzer.GetScanMetadataAsync(codebase.FullName, ct);
                        var sizeMb = metadata.TotalBytes / (1024.0 * 1024.0);
                        Console.Error.WriteLine(
                            $"[GauntletCI] Full codebase scan: {metadata.FileCount} files ({sizeMb:F1} MB, " +
                            $"~{metadata.EstimatedLinesOfCode:N0} lines), estimated {metadata.EstimatedSeconds:F1}s");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[GauntletCI] Warning: could not estimate scan size: {ex.Message}");
                    }
                }

                var diff = diffFile is not null
                    ? DiffParser.FromFile(diffFile.FullName)
                    : commit is not null
                        ? await DiffParser.FromGitAsync(repo.FullName, commit, config.DiffContextLines, ct)
                        : staged
                            ? await DiffParser.FromStagedAsync(repo.FullName, config.DiffContextLines, ct)
                            : unstaged
                                ? await DiffParser.FromUnstagedAsync(repo.FullName, config.DiffContextLines, ct)
                                : allChanges
                                    ? await DiffParser.FromAllChangesAsync(repo.FullName, config.DiffContextLines, ct)
                                    : codebase is not null
                                        ? await CodebaseAnalyzer.ScanDirectoryAsync(codebase.FullName, ct)
                                        : DiffParser.Parse(await Console.In.ReadToEndAsync(ct));

                // Merge config defaults: CLI value wins when explicitly passed; config fills in the rest.
                withLlm = withLlm || (config.Llm?.Enabled == true);
                withExpertCtx = withExpertCtx || (config.Llm?.ExpertContext == true);
                if (ctx.ParseResult.FindResultFor(withTicketCtxFlag) is null)
                    withTicketCtx = config.TicketProvider.Enabled;
                if (ctx.ParseResult.FindResultFor(githubPrCommentsFlag) is null)
                    ghPrComments = config.Ci.PrComments;
                if (ctx.ParseResult.FindResultFor(githubAnnotationsFlag) is null)
                    ghAnnotate = config.Ci.Annotations;
                if (ctx.ParseResult.FindResultFor(githubChecksFlag) is null)
                    githubChecks = config.Ci.Checks;
                if (ctx.ParseResult.FindResultFor(withCoverageFlag) is null)
                    withCoverage = config.Ci.Coverage;
                if (ctx.ParseResult.FindResultFor(verboseFlag) is null)
                    verbose = config.Output.Verbose;

                notifySlack ??= config.Notifications.SlackWebhook;
                notifyTeams ??= config.Notifications.TeamsWebhook;

                var useBaseline = !noBaseline && BaselineStore.Exists(repo.FullName);
                var engineeringPolicyWillRun =
                    config.Experimental.EngineeringPolicy.Enabled && withLlm;
                var requiredTier = PaidFeatureGate.ComputeAnalyzeRequiredTier(
                    withLlm, withExpertCtx, ghPrComments, githubChecks,
                    withTicketCtx, withCoverage,
                    !string.IsNullOrWhiteSpace(notifySlack),
                    !string.IsNullOrWhiteSpace(notifyTeams),
                    useBaseline, engineeringPolicyWillRun);

                var licenseEnvVar = config.Llm?.LicenseKeyEnv ?? "GAUNTLETCI_LICENSE";
                var licenseExit = await PaidFeatureGate.TryEnsureTierAsync(
                    requiredTier > LicenseTier.Community,
                    requiredTier,
                    "analyze with the selected options",
                    licenseEnvVar,
                    ct);
                if (licenseExit is int exitCode)
                {
                    ctx.ExitCode = exitCode;
                    return;
                }

                // Severity: only apply config value if user did not explicitly pass --severity
                if (ctx.ParseResult.FindResultFor(severityOption) is null)
                    severityStr = config.Output.MinSeverity;

                // Sensitivity: only apply config value if user did not explicitly pass --sensitivity
                if (ctx.ParseResult.FindResultFor(sensitivityOption) is null && !string.IsNullOrEmpty(config.Output.Sensitivity))
                    sensitivityStr = config.Output.Sensitivity;

                // Output format: resolved before banner so the correct writer is selected
                if (ctx.ParseResult.FindResultFor(outputOption) is null && config.Output.Format != "text")
                    output = config.Output.Format;

                CliBanner.PrintIfEnabled(new BannerContext
                {
                    NoBanner = noBanner,
                    OutputFormat = output ?? "text",
                });

                var ignoreList = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repo.FullName);

                // Run static analysis on changed C# files (null when no repo path or no .cs changes)
                var repoPath = repo.FullName;
                var sw = Stopwatch.StartNew();
                var staticAnalysis = await StaticAnalysisRunner.RunAsync(diff, repoPath, ct);

                var result = await orchestrator.RunAsync(diff, staticAnalysis, ignoreList: ignoreList, ct: ct);

                // Enrichment pipeline: automatically enrich findings with code snippets, expert context, etc.
                try
                {
                    var enrichmentPipeline = EnrichmentPipelineFactory.CreateDefault(llmEngine: null);
                    result = await result.EnrichAsync(enrichmentPipeline);
                }
                catch (Exception ex)
                {
                    // Enrichment failures should not break analysis - log and continue
                    Console.Error.WriteLine($"[enrichment] Pipeline failed: {ex.Message}");
                }

                // Ensure result is not null (it shouldn't be, but satisfy the compiler)
                if (result == null)
                {
                    Console.Error.WriteLine("[error] Analysis returned no results. Aborting.");
                    ctx.ExitCode = 1;
                    return;
                }

                // Baseline delta mode: suppress findings whose fingerprint is in the baseline.
                int suppressedByBaseline = 0;
                if (!noBaseline)
                {
                    var baseline = BaselineStore.Load(repo.FullName);
                    if (baseline is not null)
                    {
                        suppressedByBaseline = result.Findings.RemoveAll(
                            f => baseline.Fingerprints.Contains(BaselineStore.ComputeFingerprint(f)));
                    }
                }

                // Ticket context enrichment
                if (withTicketCtx && result.Findings.Count > 0)
                {
                    string? branchName = null;
                    try
                    {
                        using var gitProc = new Process
                        {
                            StartInfo = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
                            {
                                WorkingDirectory = repo.FullName,
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                            }
                        };
                        gitProc.Start();
                        branchName = (await gitProc.StandardOutput.ReadToEndAsync(ct)).Trim();
                        await gitProc.WaitForExitAsync(ct);
                    }
                    catch (Exception ex) { Console.Error.WriteLine($"[ticket] Branch detection failed: {ex.Message}"); }

                    await TicketResolver.AnnotateFindingsAsync(branchName, result.Findings, ct);
                }

                using ILlmEngine llm = await LlmEngineSelector.ResolveAsync(config, withLlm && !noLlm);

                var isJsonOutput = (output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase);
                var isHtmlOutput = (output ?? "text").Equals("html", StringComparison.OrdinalIgnoreCase);
                var isSarifOutput = (output ?? "text").Equals("sarif", StringComparison.OrdinalIgnoreCase);
                var showSpinner = llm.IsAvailable && !isJsonOutput && !isHtmlOutput && !isSarifOutput && !Console.IsOutputRedirected;

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
                            // When ticket context is attached, also adjudicate intent alignment
                            if (withTicketCtx && finding.TicketContext is not null)
                            {
                                var ticketNote = await EnrichWithTicketContextAsync(llm, finding, ct);
                                if (!string.IsNullOrWhiteSpace(ticketNote))
                                    finding.LlmExplanation = $"{finding.LlmExplanation} | Ticket: {ticketNote}";
                            }
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
                            using var store = new Llm.Embeddings.VectorStore(vectorDbPath);
                            var embedUrl = config.Llm?.EmbeddingOllamaUrl ?? "http://localhost:11434";
                            var embedModel = config.Llm?.Model ?? LlmDefaults.OllamaModel;
                            using var embedEng = new Llm.Embeddings.OllamaEmbeddingEngine(embedModel, embedUrl);
                            var adjudicator = new Llm.Embeddings.LlmAdjudicator(embedEng, store);
                            await adjudicator.AdjudicateAsync(result.Findings, ct);
                        }
                    }

                    // Engineering policy evaluation (experimental -- config-driven, LLM-required)
                    if (config.Experimental.EngineeringPolicy.Enabled && llm.IsAvailable)
                    {
                        setStatus("Checking standards...");
                        if (!RepoPathResolver.TryResolvePathUnderRoot(
                                repo.FullName, config.Experimental.EngineeringPolicy.Path, out var policyPath))
                        {
                            Console.Error.WriteLine(
                                "[GauntletCI] Engineering policy path must resolve inside the repository root. Skipping policy evaluation.");
                        }
                        else
                        {
                            var license = LicenseService.Load(config.Llm?.LicenseKeyEnv ?? "GAUNTLETCI_LICENSE");
                            var policyFindings = await EngineeringPolicyEvaluator.EvaluateAsync(
                                diff, policyPath, llm, license.IsLicensed,
                                config.Experimental.EngineeringPolicy.MaxDiffChars,
                                ctx.GetCancellationToken());
                            result.Findings.AddRange(policyFindings);
                        }
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

                if (withCoverage)
                    await CoverageCorrelator.AnnotateAsync(result, ct);

                sw.Stop();

                // If outputFile is specified, redirect output to that file
                TextWriter? fileWriter = null;
                try
                {
                    if (outputFile is not null)
                    {
                        // Ensure directory exists
                        var outputDir = Path.GetDirectoryName(outputFile);
                        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        fileWriter = new StreamWriter(File.Create(outputFile), System.Text.Encoding.UTF8);
                        // Temporarily replace Console.Out
                        var originalOut = Console.Out;
                        Console.SetOut(fileWriter);
                    }

                    if (isSarifOutput)
                    {
                        SarifWriter.Write(result);
                    }
                    else if (isHtmlOutput)
                    {
                        HtmlWriter.Write(result);
                    }
                    else if ((output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Always emit a consistent schema regardless of baseline suppression.
                        // RuleMetrics FindingCount is recomputed from remaining (non-suppressed) findings.
                        var jsonResult = new
                        {
                            result.CommitSha,
                            result.HasFindings,
                            Findings = result.Findings.Select(f => new
                            {
                                f.RuleId,
                                f.RuleName,
                                f.Summary,
                                f.Evidence,
                                f.WhyItMatters,
                                f.SuggestedAction,
                                Confidence = NormalizeConfidence(f.Confidence),
                                f.Severity,
                                f.FilePath,
                                f.Line,
                                f.CodeSnippet,
                            }).ToList(),
                            result.RulesEvaluated,
                            RuleMetrics = result.RuleMetrics.Select(m => new
                            {
                                m.RuleId,
                                m.DurationMs,
                                m.Outcome,
                                FindingCount = result.Findings.Count(f => f.RuleId == m.RuleId),
                            }).ToList(),
                            result.FileStatistics,
                            SuppressedByBaseline = suppressedByBaseline,
                            Delivery = result.DeliverySummary is null ? null : new
                            {
                                result.DeliverySummary.InputCount,
                                result.DeliverySummary.OutputCount,
                                NotShown = result.DeliverySummary.InputCount - result.DeliverySummary.OutputCount,
                                result.DeliverySummary.DroppedByGlobalCap,
                                result.DeliverySummary.DroppedByPerRuleCap,
                                result.DeliverySummary.DroppedByFileLevelDemotion,
                                result.DeliverySummary.DroppedByProvenanceFilter,
                                result.DeliverySummary.DroppedByDomainFilter,
                                result.DeliverySummary.CoordinationBoostsApplied,
                                result.DeliverySummary.SemanticsBoostsApplied,
                            },
                        };
                        var json = JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(json);
                    }
                    else
                    {
                        var minSeverity = verbose
                            ? RuleSeverity.Info
                            : ParseMinSeverity(severityStr);
                        var sensitivity = ParseSensitivity(sensitivityStr);
                        ConsoleReporter.Report(result, ascii, minSeverity, suppressedByBaseline, diff, showContext, sw.Elapsed, sensitivity);
                    }
                }
                finally
                {
                    if (fileWriter is not null)
                    {
                        Console.SetOut(TextWriter.Null); // Restore before disposing
                        fileWriter.Dispose();
                        if (outputFile is not null)
                        {
                            Console.Error.WriteLine($"[GauntletCI] Report saved to: {Path.GetFullPath(outputFile)}");
                        }
                    }
                }

                if (prCommentSuggest)
                {
                    var groups = FindingGrouper.Group(result.Findings);
                    var inlineGroups = groups
                        .Where(g => !string.IsNullOrEmpty(g.FilePath) && g.PrimaryLine.HasValue)
                        .ToList();
                    var summaryGroups = groups
                        .Where(g => string.IsNullOrEmpty(g.FilePath) || !g.PrimaryLine.HasValue)
                        .ToList();
                    foreach (var g in inlineGroups)
                    {
                        Console.WriteLine($"### {g.FilePath}:{g.PrimaryLine}");
                        Console.WriteLine();
                        Console.WriteLine(GitHubPrReviewWriter.BuildCommentBody(g));
                        Console.WriteLine();
                        Console.WriteLine("---");
                        Console.WriteLine();
                    }
                    if (summaryGroups.Count > 0)
                    {
                        Console.WriteLine("### Summary Comment");
                        Console.WriteLine();
                        Console.WriteLine(GitHubPrReviewWriter.BuildReviewBody(summaryGroups, hasInlineComments: inlineGroups.Count > 0));
                    }
                }

                if (ghAnnotate)
                    GitHubAnnotationWriter.Write(result);

                // Skip posting to GitHub API when --pr-comment-suggest is active (it acts as a dry-run)
                if (ghPrComments && !prCommentSuggest)
                    await GitHubPrReviewWriter.WriteAsync(result, ctx.GetCancellationToken());

                if (githubChecks)
                    await GitHubChecksWriter.WriteAsync(result, ct);

                var slackUrl = notifySlack ?? Environment.GetEnvironmentVariable("GAUNTLETCI_SLACK_WEBHOOK");
                var teamsUrl = notifyTeams ?? Environment.GetEnvironmentVariable("GAUNTLETCI_TEAMS_WEBHOOK");
                if (slackUrl is not null || teamsUrl is not null)
                    await SlackTeamsNotifier.NotifyAsync(result, slackUrl, teamsUrl, ct);

                await TelemetryCollector.CollectAsync(result, diff, repo.FullName, ct: ct);

                // Append full-detail entry to local audit log
                var diffSource = diffFile is not null ? "file"
                    : commit is not null ? "commit"
                    : staged ? "staged"
                    : unstaged ? "unstaged"
                    : allChanges ? "all-changes"
                    : "stdin";

                await AuditLog.AppendAsync(new AuditLogEntry
                {
                    RepoPath = repo.FullName,
                    CommitSha = result.CommitSha,
                    DiffSource = diffSource,
                    FilesChanged = result.FileStatistics.TotalFiles,
                    FilesEligible = result.FileStatistics.EligibleFiles,
                    RulesEvaluated = result.RulesEvaluated,
                    FindingCount = result.Findings.Count,
                    Findings = [.. result.Findings.Select(f => new AuditFinding
                    {
                        RuleId     = f.RuleId,
                        RuleName   = f.RuleName,
                        Summary    = f.Summary,
                        Confidence = f.Confidence.ToString(),
                        FilePath   = f.FilePath,
                        Line       = f.Line,
                    })],
                }, ct);

                ctx.ExitCode = ShouldBlockWithSensitivity(result, config.ExitOn, ParseSensitivity(sensitivityStr)) ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Error: {ex.Message}");
                ctx.ExitCode = 2;
            }
        });

        return cmd;
    }

    private static RuleSeverity ParseMinSeverity(string s) =>
        s.ToLowerInvariant() switch
        {
            "block" => RuleSeverity.Block,
            "info" => RuleSeverity.Info,
            _ => RuleSeverity.Warn,
        };

    private static SensitivityThreshold ParseSensitivity(string s) =>
        s.ToLowerInvariant() switch
        {
            "strict" => SensitivityThreshold.Strict,
            "permissive" => SensitivityThreshold.Permissive,
            _ => SensitivityThreshold.Balanced,
        };

    private static bool ShouldBlockWithSensitivity(EvaluationResult result, string? exitOn, SensitivityThreshold sensitivity)
    {
        var blockable = result.Findings.Where(f =>
            SensitivityFilter.Passes(f.Severity, f.Confidence, sensitivity));
        return exitOn?.Equals("Warn", StringComparison.OrdinalIgnoreCase) == true
            ? blockable.Any(f => f.Severity is RuleSeverity.Warn or RuleSeverity.Block)
            : blockable.Any(f => f.Severity == RuleSeverity.Block);
    }

    private static async Task<string?> EnrichWithTicketContextAsync(ILlmEngine llm, Finding finding, CancellationToken ct)
    {
        var ticket = finding.TicketContext!;
        var prompt =
            $"Ticket {ticket.Id}: \"{ticket.Title}\". {ticket.Description}\n\n" +
            $"Finding: {finding.Summary}. {finding.WhyItMatters}\n\n" +
            $"Does this change align with the stated ticket intent? " +
            $"If yes, state 'Change aligns with ticket intent: {ticket.Id}' and suggest a downgrade rationale. " +
            $"If no, explain briefly. Maximum 30 words.";
        try
        {
            return await llm.CompleteAsync(prompt, ct);
        }
        catch
        {
            return null;
        }
    }

    private static double NormalizeConfidence(Confidence confidence)
    {
        return confidence switch
        {
            Confidence.Low => 0.33,
            Confidence.Medium => 0.66,
            Confidence.High => 0.95,
            _ => 0.0,
        };
    }
}
