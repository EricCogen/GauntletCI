// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Cli.Analysis;
using GauntletCI.Cli.Audit;
using GauntletCI.Cli.Baseline;
using GauntletCI.Cli.LlmDaemon;
using GauntletCI.Cli.Output;
using GauntletCI.Cli.Presentation;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Cli.TicketProviders;
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
            "Output format: text, json, or sarif");
        var noLlmFlag = new Option<bool>("--no-llm", "Disable LLM enrichment (deprecated — LLM is now opt-in via --with-llm)") { IsHidden = true };
        var withLlmFlag = new Option<bool>("--with-llm", "Enable LLM enrichment of High-confidence findings (requires 'gauntletci model download', adds latency)");
        var asciiFlag = new Option<bool>("--ascii", "Use ASCII-only output (for terminals without Unicode support)");
        var noBannerOption = new Option<bool>("--no-banner", "Disable ASCII banner");
        var githubAnnotationsFlag = new Option<bool>("--github-annotations", "Emit GitHub Actions workflow commands for inline PR annotations");
        var githubPrCommentsFlag  = new Option<bool>("--github-pr-comments",
            "Post findings as a GitHub PR review with inline comments (requires pull-requests: write permission)");
        var withExpertCtxFlag = new Option<bool>("--with-expert-context",
            "Attach matching expert facts from the local vector store to findings (requires 'gauntletci llm seed')");
        var verboseFlag = new Option<bool>("--verbose", "Show Info-severity findings in addition to Warn and Block");
        var severityOption = new Option<string>(
            "--severity",
            () => "warn",
            "Minimum severity to display: info, warn, block");
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
            var ghAnnotate    = ctx.ParseResult.GetValueForOption(githubAnnotationsFlag);
            var ghPrComments  = ctx.ParseResult.GetValueForOption(githubPrCommentsFlag);
            var withExpertCtx = ctx.ParseResult.GetValueForOption(withExpertCtxFlag);
            var verbose    = ctx.ParseResult.GetValueForOption(verboseFlag);
            var severityStr = ctx.ParseResult.GetValueForOption(severityOption)!;
            var noBaseline = ctx.ParseResult.GetValueForOption(noBaselineFlag);
            var showContext = ctx.ParseResult.GetValueForOption(showContextOption);
            var prCommentSuggest = ctx.ParseResult.GetValueForOption(prCommentSuggestFlag);
            var withCoverage  = ctx.ParseResult.GetValueForOption(withCoverageFlag);
            var githubChecks  = ctx.ParseResult.GetValueForOption(githubChecksFlag);
            var withTicketCtx = ctx.ParseResult.GetValueForOption(withTicketCtxFlag);
            var notifySlack   = ctx.ParseResult.GetValueForOption(notifySlackOption);
            var notifyTeams   = ctx.ParseResult.GetValueForOption(notifyTeamsOption);
            var ct = ctx.GetCancellationToken();

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

            if (sourceCount == 0 && !Console.IsInputRedirected)
            {
                Console.Error.WriteLine("[GauntletCI] Error: no diff source specified. Use --staged, --unstaged, --all-changes, --commit <sha>, --diff <file>, or pipe a diff to stdin.");
                ctx.ExitCode = 1;
                return;
            }

            if (prCommentSuggest && ("json".Equals(output, StringComparison.OrdinalIgnoreCase)
                || "sarif".Equals(output, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("[GauntletCI] Error: --pr-comment-suggest cannot be combined with --output json (produces invalid output). Use --output text or omit --output.");
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

                var config = ConfigLoader.Load(repo.FullName);
                config.Ci            ??= new();
                config.Output        ??= new();
                config.Notifications ??= new();
                config.TicketProvider ??= new();

                // Merge config defaults — CLI value wins when explicitly passed; config fills in the rest.
                withLlm       = withLlm       || (config.Llm?.Enabled      == true);
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

                // Severity: only apply config value if user did not explicitly pass --severity
                if (ctx.ParseResult.FindResultFor(severityOption) is null)
                    severityStr = config.Output.MinSeverity;

                // Output format: resolved before banner so the correct writer is selected
                if (ctx.ParseResult.FindResultFor(outputOption) is null && config.Output.Format != "text")
                    output = config.Output.Format;

                CliBanner.PrintIfEnabled(new BannerContext
                {
                    NoBanner = noBanner,
                    OutputFormat = output ?? "text",
                });

                // Notifications: CLI arg takes precedence, then config, then env var (handled downstream)
                notifySlack ??= config.Notifications.SlackWebhook;
                notifyTeams ??= config.Notifications.TeamsWebhook;

                var ignoreList = IgnoreList.Load(repo.FullName);
                var orchestrator = RuleOrchestrator.CreateDefault(config, repoPath: repo.FullName);

                // Run static analysis on changed C# files (null when no repo path or no .cs changes)
                var repoPath = diffFile is null ? repo.FullName : null;
                var staticAnalysis = await StaticAnalysisRunner.RunAsync(diff, repoPath, ct);

                var result = await orchestrator.RunAsync(diff, staticAnalysis, ignoreList: ignoreList);

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
                        using var gitProc = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
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

                var isJsonOutput  = (output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase);
                var isSarifOutput = (output ?? "text").Equals("sarif", StringComparison.OrdinalIgnoreCase);
                var showSpinner   = llm.IsAvailable && !isJsonOutput && !isSarifOutput && !Console.IsOutputRedirected;

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
                            using var store    = new GauntletCI.Llm.Embeddings.VectorStore(vectorDbPath);
                            var embedUrl       = config.Llm?.EmbeddingOllamaUrl ?? "http://localhost:11434";
                            var embedModel     = config.Llm?.Model ?? LlmDefaults.OllamaModel;
                            using var embedEng = new GauntletCI.Llm.Embeddings.OllamaEmbeddingEngine(embedModel, embedUrl);
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

                if (withCoverage)
                    await CoverageCorrelator.AnnotateAsync(result, ct);

                if (isSarifOutput)
                {
                    SarifWriter.Write(result);
                }
                else if ((output ?? "text").Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // Always emit a consistent schema regardless of baseline suppression.
                    // RuleMetrics FindingCount is recomputed from remaining (non-suppressed) findings.
                    var jsonResult = new
                    {
                        result.CommitSha,
                        result.HasFindings,
                        result.Findings,
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
                    };
                    var json = JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);
                }
                else
                {
                    var minSeverity = verbose
                        ? GauntletCI.Core.Model.RuleSeverity.Info
                        : ParseMinSeverity(severityStr);
                    ConsoleReporter.Report(result, ascii, minSeverity, suppressedByBaseline, diff, showContext);
                }

                if (prCommentSuggest)
                {
                    var inlineFindings = result.Findings.Where(f => !string.IsNullOrEmpty(f.FilePath) && f.Line.HasValue).ToList();
                    var summaryFindings = result.Findings.Where(f => string.IsNullOrEmpty(f.FilePath) || !f.Line.HasValue).ToList();
                    foreach (var finding in inlineFindings)
                    {
                        Console.WriteLine($"### {finding.FilePath}:{finding.Line}");
                        Console.WriteLine();
                        Console.WriteLine(GitHubPrReviewWriter.BuildCommentBody(finding));
                        Console.WriteLine();
                        Console.WriteLine("---");
                        Console.WriteLine();
                    }
                    if (summaryFindings.Count > 0)
                    {
                        Console.WriteLine("### Summary Comment");
                        Console.WriteLine();
                        Console.WriteLine(GitHubPrReviewWriter.BuildReviewBody(summaryFindings, hasInlineComments: inlineFindings.Count > 0));
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
                }, ct);

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
}
