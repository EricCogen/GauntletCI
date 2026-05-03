// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Factory for basic corpus operations: add-pr, normalize, list, show, status, batch-hydrate.
/// Handles command-line argument parsing and option configuration for simple corpus workflows.
/// 
/// Each Create* method builds and returns a System.CommandLine Command with:
/// - Option definitions (--flag descriptions)
/// - SetHandler callback for async execution
/// - Error handling and exit code management
/// 
/// Extracted from CorpusCommand to improve maintainability (EI-4, EI-5 compliance).
/// Target: &lt;600 LOC, single responsibility, &lt;30 LOC per method.
/// </summary>
public static class CorpusOperationsFactory
{
    /// <summary>
    /// Create the 'add-pr' command: Hydrate a single PR from GitHub URL and add to corpus.
    /// Command: corpus add-pr --url &lt;url&gt; [--db &lt;path&gt;] [--fixtures &lt;path&gt;]
    /// </summary>
    public static Command CreateAddPr()
    {
        var urlOpt      = new Option<string>("--url",      "GitHub PR URL (https://github.com/owner/repo/pull/NNN)") { IsRequired = true };
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("add-pr", "Hydrate a pull request and add it to the corpus");
        cmd.AddOption(urlOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var url      = ctx.ParseResult.GetValueForOption(urlOpt)!;
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct       = ctx.GetCancellationToken();

            Console.WriteLine($"[corpus] Hydrating {url}");

            var (db, _, pipeline) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    using var hydrator = GitHubRestHydrator.CreateDefault(fixtures);
                    var hydrated = await hydrator.HydrateFromUrlAsync(url, ct);
                    var metadata = await pipeline.NormalizeAsync(hydrated, source: "manual", ct: ct);

                    CorpusCommandHelpers.PrintMetadata(metadata);

                    var fixtureId = FixtureIdHelper.Build(
                        hydrated.RepoOwner, hydrated.RepoName, hydrated.PullRequestNumber);
                    using var enricher = IssueEnricher.CreateDefault();
                    var linked = await enricher.EnrichAsync(
                        db.Connection, fixtureId,
                        hydrated.RepoOwner, hydrated.RepoName, hydrated.Body, ct);
                    if (linked > 0) Console.WriteLine($"[corpus] Linked {linked} issue(s) to fixture");
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
    /// Create the 'normalize' command: Re-normalize an existing fixture from raw snapshots.
    /// Command: corpus normalize --fixture &lt;id&gt; [--owner] [--repo] [--pr] [--tier] [--db] [--fixtures]
    /// </summary>
    public static Command CreateNormalize()
    {
        var fixtureOpt  = new Option<string>("--fixture",  "Fixture ID (e.g. owner_repo_pr1234)") { IsRequired = true };
        var tierOpt     = new Option<string>("--tier",     () => "discovery", "Fixture tier (gold|silver|discovery)");
        var ownerOpt    = new Option<string>("--owner",    "Repo owner override");
        var repoOpt     = new Option<string>("--repo",     "Repo name override");
        var prOpt       = new Option<int>   ("--pr",       "PR number override");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("normalize", "Re-normalize a fixture from its existing raw/ snapshots");
        cmd.AddOption(fixtureOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(ownerOpt);
        cmd.AddOption(repoOpt);
        cmd.AddOption(prOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var fixtureId = ctx.ParseResult.GetValueForOption(fixtureOpt)!;
            var tierStr   = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var owner     = ctx.ParseResult.GetValueForOption(ownerOpt) ?? "";
            var repo      = ctx.ParseResult.GetValueForOption(repoOpt)  ?? "";
            var prNumber  = ctx.ParseResult.GetValueForOption(prOpt);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var tier))
            {
                Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                ctx.ExitCode = 1;
                return;
            }

            var (db, store, pipeline) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo) || prNumber == 0)
                {
                    var existingMeta = await store.GetMetadataAsync(fixtureId, ct);
                    if (existingMeta is not null)
                    {
                        var repoParts = existingMeta.Repo.Split('/', 2);
                        owner    = string.IsNullOrEmpty(owner)   ? repoParts[0] : owner;
                        repo     = string.IsNullOrEmpty(repo)    ? (repoParts.Length > 1 ? repoParts[1] : "") : repo;
                        prNumber = prNumber == 0 ? existingMeta.PullRequestNumber : prNumber;
                    }
                }

                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo) || prNumber == 0)
                {
                    Console.Error.WriteLine("[corpus] Could not determine owner/repo/pr from metadata. Provide --owner, --repo, --pr.");
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"[corpus] Re-normalizing {fixtureId} ({tier})");

                try
                {
                    var metadata = await pipeline.ReNormalizeFromRawAsync(
                        fixtureId, tier, owner, repo, prNumber, ct);
                    CorpusCommandHelpers.PrintMetadata(metadata);
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
    /// Create the 'list' command: Enumerate and filter corpus fixtures by tier, language, or tags.
    /// Command: corpus list [--tier] [--language] [--tag] [--output] [--db] [--fixtures]
    /// </summary>
    public static Command CreateList()
    {
        var tierOpt     = new Option<string?>("--tier",     "Filter by tier (gold|silver|discovery)");
        var languageOpt = new Option<string?>("--language", "Filter by language (e.g. cs, py)");
        var tagOpt      = new Option<string[]>("--tag",     "Filter by tag (repeatable or comma-separated)")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore,
        };
        var outputOpt   = new Option<string>("--output",   () => "text", "Output format: text or json");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("list", "Enumerate and filter corpus fixtures");
        cmd.AddOption(tierOpt);
        cmd.AddOption(languageOpt);
        cmd.AddOption(tagOpt);
        cmd.AddOption(outputOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr  = ctx.ParseResult.GetValueForOption(tierOpt);
            var language = ctx.ParseResult.GetValueForOption(languageOpt);
            var tags     = ctx.ParseResult.GetValueForOption(tagOpt) ?? [];
            var output   = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct       = ctx.GetCancellationToken();

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

            var expandedTags = tags
                .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                var all = await store.ListFixturesAsync(tier, ct);

                var filtered = all.AsEnumerable();
                if (!string.IsNullOrEmpty(language))
                    filtered = filtered.Where(m => m.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (expandedTags.Length > 0)
                    filtered = filtered.Where(m => expandedTags.All(tag => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));

                var results = filtered.ToList();

                if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    CorpusCommandHelpers.PrintAsJson(results);
                }
                else
                {
                    CorpusCommandHelpers.PrintFixtureTable(results);
                }
            }
        });

        return cmd;
    }

    /// <summary>
    /// Create the 'show' command: Display detailed metadata and findings for a fixture.
    /// Command: corpus show &lt;fixture-id&gt; [--db] [--fixtures]
    /// </summary>
    public static Command CreateShow()
    {
        var fixtureArg  = new Argument<string>("fixture-id", "Fixture ID to inspect");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("show", "Inspect a single corpus fixture: metadata and findings");
        cmd.AddArgument(fixtureArg);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var fixtureId = ctx.ParseResult.GetValueForArgument(fixtureArg);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var meta = await store.GetMetadataAsync(fixtureId, ct);
                    if (meta is null)
                    {
                        Console.Error.WriteLine($"[corpus] Fixture '{fixtureId}' not found.");
                        ctx.ExitCode = 1;
                        return;
                    }

                    var sep = new string('─', 60);
                    Console.WriteLine(sep);
                    Console.WriteLine($"  Fixture : {meta.FixtureId}");
                    Console.WriteLine($"  Tier    : {meta.Tier}");
                    var repoParts = meta.Repo.Split('/', 2);
                    var owner     = repoParts.Length > 1 ? repoParts[0] : meta.Repo;
                    var repoName  = repoParts.Length > 1 ? repoParts[1] : meta.Repo;
                    Console.WriteLine($"  PR      : https://github.com/{owner}/{repoName}/pull/{meta.PullRequestNumber}");
                    Console.WriteLine($"  Size    : {meta.PrSizeBucket} ({meta.FilesChanged} files changed)");
                    Console.WriteLine($"  Language: {meta.Language}");
                    Console.WriteLine($"  Tags    : {(meta.Tags.Count > 0 ? string.Join(", ", meta.Tags) : "(none)")}");
                    Console.WriteLine(sep);

                    var actual = await store.ReadActualFindingsAsync(fixtureId, ct);
                    if (actual.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("  ACTUAL FINDINGS");
                        foreach (var f in actual.OrderBy(f => f.RuleId))
                            Console.WriteLine($"    {f.RuleId}: {(f.DidTrigger ? "YES" : "no")}");
                    }

                    Console.WriteLine(sep);
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
    /// Create the 'status' command: Display corpus statistics (fixture counts, disk size, languages).
    /// Command: corpus status [--db] [--fixtures]
    /// </summary>
    public static Command CreateStatus()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("status", "Display corpus size and fixture statistics");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    Console.WriteLine("\n=== Corpus Status ===\n");

                    var allFixtures = await store.ListFixturesAsync(null, ct);
                    var byTier = allFixtures
                        .GroupBy(m => m.Tier)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var totalSize = 0L;
                    var fixtureDir = new DirectoryInfo(fixtures);
                    if (fixtureDir.Exists)
                    {
                        totalSize = fixtureDir.EnumerateFiles("*", SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                    }

                    Console.WriteLine($"Total fixtures  : {allFixtures.Count}");
                    if (byTier.TryGetValue(FixtureTier.Gold, out var goldCount))
                        Console.WriteLine($"  Gold tier   : {goldCount}");
                    if (byTier.TryGetValue(FixtureTier.Silver, out var silverCount))
                        Console.WriteLine($"  Silver tier : {silverCount}");
                    if (byTier.TryGetValue(FixtureTier.Discovery, out var discoveryCount))
                        Console.WriteLine($"  Discovery   : {discoveryCount}");

                    var languageCounts = allFixtures
                        .GroupBy(m => m.Language)
                        .OrderByDescending(g => g.Count())
                        .Take(5);

                    Console.WriteLine($"\nTop languages:");
                    foreach (var lang in languageCounts)
                        Console.WriteLine($"  {lang.Key,10} : {lang.Count(),4}");

                    var sizeGb = totalSize / (1024.0 * 1024.0 * 1024.0);
                    Console.WriteLine($"\nTotal disk size : {sizeGb:F2} GB");
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
    /// Create the 'batch-hydrate' command: Bulk hydrate fixtures from GitHub with progress.
    /// Command: corpus batch-hydrate [--tier] [--limit] [--delay-ms] [--db] [--fixtures]
    /// </summary>
    public static Command CreateBatchHydrate()
    {
        var tierOpt     = new Option<string>("--tier",     () => "discovery", "Target tier for hydration (gold|silver|discovery)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,           "Max fixtures to hydrate (0 = unlimited)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 100,         "Delay between GitHub API calls (ms)");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("batch-hydrate", "Bulk hydrate corpus fixtures from GitHub search results");
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr   = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit     = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs   = ctx.ParseResult.GetValueForOption(delayOpt);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var tier))
            {
                Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                ctx.ExitCode = 1;
                return;
            }

            var (db, store, pipeline) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var metadata = await store.ListFixturesAsync(tier, ct);
                    var toHydrate = metadata
                        .Take(limit > 0 ? limit : int.MaxValue)
                        .ToList();

                    if (toHydrate.Count == 0)
                    {
                        Console.WriteLine($"[corpus] No {tier} fixtures found.");
                        return;
                    }

                    Console.WriteLine($"[corpus] Found {toHydrate.Count} {tier} fixtures. Hydrating...");
                    Console.WriteLine();

                    using var hydrator = GitHubRestHydrator.CreateDefault(fixtures);

                    var hydratedCount = 0;
                    var errorCount = 0;

                    foreach (var fixture in toHydrate)
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            var url = $"https://github.com/{fixture.Repo}/pull/{fixture.PullRequestNumber}";
                            Console.Write($"  [{hydratedCount + 1}/{toHydrate.Count}] {fixture.FixtureId}... ");

                            var hydrated = await hydrator.HydrateFromUrlAsync(url, ct);
                            var norm = await pipeline.NormalizeAsync(hydrated, source: fixture.Tier.ToString().ToLower(), ct: ct);

                            Console.WriteLine("✓");
                            hydratedCount++;

                            if (delayMs > 0) await Task.Delay(delayMs, ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"✗ ({ex.Message})");
                            errorCount++;
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine($"[corpus] Batch hydration complete: {hydratedCount} succeeded, {errorCount} failed");
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
