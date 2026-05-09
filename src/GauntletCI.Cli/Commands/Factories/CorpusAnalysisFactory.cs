// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Discovery;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Scoring;
using GauntletCI.Corpus.Storage;
using GauntletCI.Core.Configuration;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Factory for corpus analysis and evaluation: discover, run, run-all, score, report.
/// Handles discovery of PR candidates, rule execution, performance scoring, and reporting.
/// 
/// Each Create* method builds and returns a System.CommandLine Command with:
/// - Option definitions and filtering logic
/// - SetHandler callback for async analysis operations
/// - Result aggregation and formatted output
/// 
/// Extracted from CorpusCommand to improve maintainability (EI-4, EI-5 compliance).
/// Target: <600 LOC, single responsibility, focused on discovery & evaluation.
/// </summary>
public static class CorpusAnalysisFactory
{
    /// <summary>
    /// Create the 'discover' command: Find PR candidates from GitHub and add to corpus database.
    /// Command: corpus discover --provider &lt;provider&gt; [--limit] [--language] [--min-stars] [--repo-allowlist] [--db] [--fixtures]
    /// </summary>
    public static Command CreateDiscover()
    {
        var providerOpt    = new Option<string>("--provider",     "Discovery provider: gh-search or gh-archive") { IsRequired = true };
        var limitOpt       = new Option<int>   ("--limit",        () => 100,           "Maximum candidates to fetch");
        var languageOpt    = new Option<string?>("--language",    "Filter by programming language (e.g. cs, python)");
        var minStarsOpt    = new Option<int>   ("--min-stars",    () => 0,             "Minimum stars on the repository");
        var minCommentsOpt = new Option<int>   ("--min-comments", () => 0,             "Minimum review comment count");
        var startDateOpt   = new Option<DateTime?>("--start-date","Filter by merge/event date (inclusive, UTC)");
        var endDateOpt     = new Option<DateTime?>("--end-date",  "Filter by merge/event date upper bound (inclusive, UTC)");
        var repoBlocklistOpt = new Option<string[]>("--repo-blocklist", "Repos to exclude in owner/repo format (repeatable)")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };
        var repoAllowlistOpt = new Option<string[]>("--repo-allowlist", "Only discover from these repos in owner/repo format (repeatable). Required when --provider gh-search.")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };
        var perRepoLimitOpt  = new Option<int>   ("--per-repo-limit", () => 0,              "Max candidates per repo when using allowlist");
        var dbOpt          = new Option<string>("--db",           () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt    = new Option<string>("--fixtures",     () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("discover", "Discover pull request candidates and persist them to the corpus database");
        cmd.AddOption(providerOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(languageOpt);
        cmd.AddOption(minStarsOpt);
        cmd.AddOption(minCommentsOpt);
        cmd.AddOption(startDateOpt);
        cmd.AddOption(endDateOpt);
        cmd.AddOption(repoBlocklistOpt);
        cmd.AddOption(repoAllowlistOpt);
        cmd.AddOption(perRepoLimitOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var providerName = ctx.ParseResult.GetValueForOption(providerOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var language     = ctx.ParseResult.GetValueForOption(languageOpt);
            var minStars     = ctx.ParseResult.GetValueForOption(minStarsOpt);
            var minComments  = ctx.ParseResult.GetValueForOption(minCommentsOpt);
            var startDate    = ctx.ParseResult.GetValueForOption(startDateOpt);
            var endDate      = ctx.ParseResult.GetValueForOption(endDateOpt);
            var repoBlocklist  = ctx.ParseResult.GetValueForOption(repoBlocklistOpt) ?? [];
            var repoAllowlist  = ctx.ParseResult.GetValueForOption(repoAllowlistOpt) ?? [];
            var perRepoLimit   = ctx.ParseResult.GetValueForOption(perRepoLimitOpt);
            var dbPath         = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures       = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct             = ctx.GetCancellationToken();

            IDiscoveryProvider provider;
            var errorLog = new List<(string? Repo, int? Code, string Message)>();

            if (providerName.Equals("gh-search", StringComparison.OrdinalIgnoreCase))
            {
                var token = GitHubTokenResolver.Resolve();
                if (string.IsNullOrEmpty(token))
                {
                    Console.Error.WriteLine("[corpus] Error: no GitHub token found. Set GITHUB_TOKEN or run 'gh auth login'.");
                    ctx.ExitCode = 1;
                    return;
                }
                if (repoAllowlist.Length == 0)
                {
                    Console.Error.WriteLine("[corpus] Error: --repo-allowlist is required for gh-search.");
                    ctx.ExitCode = 1;
                    return;
                }
                Action<string?, int?, string> errorCallback = (repo, code, msg) => errorLog.Add((repo, code, msg));
                provider = new GitHubSearchDiscoveryProvider(token, errorCallback);
            }
            else if (providerName.Equals("gh-archive", StringComparison.OrdinalIgnoreCase))
            {
                provider = new GhArchiveDiscoveryProvider();
            }
            else
            {
                Console.Error.WriteLine($"[corpus] Unknown provider '{providerName}'. Use gh-search or gh-archive.");
                ctx.ExitCode = 1;
                return;
            }

            using var providerLifetime = provider as IDisposable;

            var languages = string.IsNullOrWhiteSpace(language)
                ? Array.Empty<string>()
                : new[] { language };

            var query = new DiscoveryQuery
            {
                Languages          = languages,
                MinStars           = minStars,
                MinReviewComments  = minComments,
                StartDateUtc       = startDate,
                EndDateUtc         = endDate,
                MaxCandidates      = limit,
                PerRepoLimit       = perRepoLimit,
                RepoBlockList      = repoBlocklist,
                RepoAllowList      = repoAllowlist,
            };

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);

            try
            {
                Console.WriteLine($"[corpus] Discovering candidates via {provider.GetProviderName()} (limit={limit}{(perRepoLimit > 0 ? $", per-repo={perRepoLimit}" : "")})...");

                var candidates = await provider.SearchCandidatesAsync(query, ct);

                Console.WriteLine($"[corpus] Found {candidates.Count} candidate(s).");

                var store = new FixtureFolderStore(db, fixtures);
                var addedCount = 0;
                var existingCount = 0;

                foreach (var candidate in candidates)
                {
                    var fixtureId = FixtureIdHelper.Build(candidate.RepoOwner, candidate.RepoName, candidate.PullRequestNumber);
                    var existing = await store.GetMetadataAsync(fixtureId, ct);

                    if (existing is not null)
                    {
                        existingCount++;
                        continue;
                    }

                    var metadata = new FixtureMetadata
                    {
                        FixtureId = fixtureId,
                        Tier = FixtureTier.Discovery,
                        Repo = $"{candidate.RepoOwner}/{candidate.RepoName}",
                        PullRequestNumber = candidate.PullRequestNumber,
                        Language = candidate.Language,
                        CreatedAtUtc = DateTime.UtcNow,
                        Source = provider.GetProviderName().ToLower(),
                    };

                    await store.SaveMetadataAsync(metadata, ct);
                    addedCount++;

                    if (addedCount % 10 == 0)
                        Console.WriteLine($"[corpus] Added {addedCount} fixture(s)...");
                }

                Console.WriteLine();
                Console.WriteLine($"[corpus] Discovery complete:");
                Console.WriteLine($"  Added    : {addedCount}");
                Console.WriteLine($"  Existing : {existingCount}");

                if (errorLog.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[corpus] Errors ({errorLog.Count}):");
                    foreach (var (repo, code, msg) in errorLog.Take(5))
                        Console.WriteLine($"  {repo}: {msg}");
                    if (errorLog.Count > 5)
                        Console.WriteLine($"  ... and {errorLog.Count - 5} more");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                ctx.ExitCode = 1;
            }
            finally
            {
                db.Dispose();
            }
        });

        return cmd;
    }

    /// <summary>
    /// Create the 'run' command: Execute GCI rules against a single fixture and save findings.
    /// Command: corpus run --fixture &lt;id&gt; [--db] [--fixtures]
    /// </summary>
    public static Command CreateRun()
    {
        var fixtureOpt  = new Option<string>("--fixture",  "Fixture ID to run rules against") { IsRequired = true };
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("run", "Run GCI rules against a single corpus fixture");
        cmd.AddOption(fixtureOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var fixtureId = ctx.ParseResult.GetValueForOption(fixtureOpt)!;
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            var configDir = CorpusCommandHelpers.FindGitRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
            var config    = ConfigLoader.Load(configDir);

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var metadata = await store.GetMetadataAsync(fixtureId, ct);
                    if (metadata is null)
                    {
                        Console.Error.WriteLine($"[corpus] Fixture '{fixtureId}' not found.");
                        ctx.ExitCode = 1;
                        return;
                    }

                    string? fixturePath = null;
                    foreach (var t in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
                    {
                        var candidate = FixtureIdHelper.GetFixturePath(fixtures, t, fixtureId);
                        if (Directory.Exists(candidate)) { fixturePath = candidate; break; }
                    }

                    var diffPath = fixturePath is not null ? Path.Combine(fixturePath, "diff.patch") : null;

                    if (fixturePath is null || !File.Exists(diffPath!))
                    {
                        Console.Error.WriteLine($"[corpus] diff.patch not found for fixture '{fixtureId}'");
                        ctx.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine($"[corpus] Running GCI rules against {fixtureId}");

                    var diffText = await File.ReadAllTextAsync(diffPath, ct);
                    var runner   = new RuleCorpusRunner(store, db, config, configDir);
                    var findings = await runner.RunAsync(fixtureId, diffText, ct);

                    int high   = findings.Count(f => f.ActualConfidence >= 1.0);
                    int medium = findings.Count(f => f.ActualConfidence is >= 0.5 and < 1.0);
                    int low    = findings.Count(f => f.ActualConfidence < 0.5);

                    Console.WriteLine($"[corpus] Run ID  : {runner.LastRunId}");
                    Console.WriteLine($"[corpus] Findings: {findings.Count} ({high} High, {medium} Medium, {low} Low)");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            }
        });

        return cmd;
    }

    /// <summary>
    /// Create the 'run-all' command: Execute GCI rules against all fixtures in a tier.
    /// Command: corpus run-all [--tier] [--db] [--fixtures]
    /// </summary>
    public static Command CreateRunAll()
    {
        var tierOpt     = new Option<string?>("--tier",     "Fixture tier to run (gold|silver|discovery)");
        var dbOpt       = new Option<string> ("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string> ("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("run-all", "Run GCI rules against all fixtures in a tier");
        cmd.AddOption(tierOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr   = ctx.ParseResult.GetValueForOption(tierOpt);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            var configDir = CorpusCommandHelpers.FindGitRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
            var config    = ConfigLoader.Load(configDir);

            FixtureTier? tier = null;
            if (!string.IsNullOrEmpty(tierStr))
            {
                if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var parsedTier))
                {
                    Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                    ctx.ExitCode = 1;
                    return;
                }
                tier = parsedTier;
            }

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var allFixtures = await store.ListFixturesAsync(tier, ct);

                    if (allFixtures.Count == 0)
                    {
                        Console.WriteLine("[corpus] No fixtures found.");
                        return;
                    }

                    int totalFindings = 0;
                    int completed     = 0;
                    int failed        = 0;

                    var runner = new RuleCorpusRunner(store, db, config, configDir);

                    foreach (var metadata in allFixtures)
                    {
                        if (ct.IsCancellationRequested) break;

                        // Search all tier folders for the fixture
                        string? fixturePath = null;
                        foreach (var t in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
                        {
                            var candidate = FixtureIdHelper.GetFixturePath(fixtures, t, metadata.FixtureId);
                            if (Directory.Exists(candidate)) { fixturePath = candidate; break; }
                        }

                        var diffPath = fixturePath is not null ? Path.Combine(fixturePath, "diff.patch") : null;

                        if (fixturePath is null || !File.Exists(diffPath!))
                        {
                            Console.WriteLine($"[corpus] SKIP {metadata.FixtureId}: diff.patch not found");
                            failed++;
                            continue;
                        }

                        try
                        {
                            var diffText = await File.ReadAllTextAsync(diffPath, ct);
                            var findings = await runner.RunAsync(metadata.FixtureId, diffText, ct);

                            totalFindings += findings.Count;
                            completed++;

                            Console.WriteLine($"[corpus] OK   {metadata.FixtureId,-40} {findings.Count,3} finding(s)");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[corpus] ERR  {metadata.FixtureId}: {ex.Message}");
                            failed++;
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine($"[corpus] Run-all complete: {completed} OK, {failed} failed, {totalFindings} total findings");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            }
        });

        return cmd;
    }

    /// <summary>
    /// Create the 'score' command: Compute rule performance scorecards from corpus results.
    /// Command: corpus score [--rule] [--tier] [--db]
    /// </summary>
    public static Command CreateScore()
    {
        var ruleOpt     = new Option<string?>("--rule",     "Filter by rule ID (e.g. GCI0001)");
        var tierOpt     = new Option<string?>("--tier",     "Filter by tier (gold|silver|discovery)");
        var dbOpt       = new Option<string> ("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string> ("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("score", "Compute rule scorecards from corpus fixture results");
        cmd.AddOption(ruleOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var ruleId   = ctx.ParseResult.GetValueForOption(ruleOpt);
            var tierStr  = ctx.ParseResult.GetValueForOption(tierOpt);
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct       = ctx.GetCancellationToken();

            FixtureTier? tier = null;
            if (!string.IsNullOrEmpty(tierStr))
            {
                if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var parsed))
                {
                    Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                    ctx.ExitCode = 1;
                    return;
                }
                tier = parsed;
            }

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var aggregator = new ScoreAggregator(store, db);
                    var scorecards = await aggregator.ScoreAsync(ruleId, tier, ct);

                    if (scorecards.Count == 0)
                    {
                        Console.WriteLine("[corpus] No scorecards: run 'corpus run-all' first to generate actual.json files.");
                        return;
                    }

                    const int colRule      = 10;
                    const int colTier      = 10;
                    const int colFixtures  = 9;
                    const int colTrigger   = 12;
                    const int colPrecision = 10;
                    const int colRecall    = 8;
                    const int colUseful    = 11;

                    var header = $"{"RuleId",-colRule}  {"Tier",-colTier}  {"Fixtures",-colFixtures}  {"TriggerRate",-colTrigger}  {"Precision",-colPrecision}  {"Recall",-colRecall}  {"Usefulness",-colUseful}";
                    var sep    = new string('-', header.Length + 4);

                    Console.WriteLine(sep);
                    Console.WriteLine(header);
                    Console.WriteLine(sep);

                    foreach (var sc in scorecards.OrderBy(s => s.RuleId).ThenBy(s => s.Tier))
                    {
                        Console.WriteLine(
                            $"{sc.RuleId,-colRule}  {sc.Tier,-colTier}  {sc.Fixtures,-colFixtures}  " +
                            $"{sc.TriggerRate,colTrigger:P1}  {sc.Precision,colPrecision:P1}  " +
                            $"{sc.Recall,colRecall:P1}  {sc.AvgUsefulness,-colUseful:F1}/5");
                    }

                    Console.WriteLine(sep);
                    Console.WriteLine($"[corpus] {scorecards.Count} scorecard(s)");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            }
        });

        return cmd;
    }

    /// <summary>
    /// Create the 'report' command: Generate summary report of corpus evaluation results.
    /// Command: corpus report [--output] [--db] [--fixtures]
    /// </summary>
    public static Command CreateReport()
    {
        var outputOpt   = new Option<string>("--output",   () => "./corpus-report.md", "Output file path for the markdown report");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("report", "Export a markdown scorecard report for all rules");
        cmd.AddOption(outputOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var outputPath = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var dbPath     = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures   = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct         = ctx.GetCancellationToken();

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var aggregator = new ScoreAggregator(store, db);
                    var exporter   = new MarkdownReportExporter(aggregator);
                    var markdown   = await exporter.ExportMarkdownAsync(ct);

                    var dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    await File.WriteAllTextAsync(outputPath, markdown, ct);
                    Console.WriteLine($"[corpus] Report written to {outputPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            }
        });

        return cmd;
    }
}
