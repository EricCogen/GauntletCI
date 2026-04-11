// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Discovery;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Storage;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Cli.Commands;

public static class CorpusCommand
{
    public static Command Create()
    {
        var corpus = new Command("corpus", "Manage the GauntletCI fixture corpus");
        corpus.AddCommand(CreateAddPr());
        corpus.AddCommand(CreateNormalize());
        corpus.AddCommand(CreateList());
        corpus.AddCommand(CreateBatchHydrate());
        corpus.AddCommand(CreateDiscover());
        corpus.AddCommand(CreateRun());
        corpus.AddCommand(CreateRunAll());
        return corpus;
    }

    // ── gauntletci corpus add-pr --url <url> ─────────────────────────────────

    private static Command CreateAddPr()
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

            var (db, _, pipeline) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    using var hydrator = GitHubRestHydrator.CreateDefault(fixtures);
                    var hydrated = await hydrator.HydrateFromUrlAsync(url, ct);
                    var metadata = await pipeline.NormalizeAsync(hydrated, source: "manual", ct: ct);

                    PrintMetadata(metadata);
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

    // ── gauntletci corpus normalize --fixture <id> ───────────────────────────

    private static Command CreateNormalize()
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

            var (db, store, pipeline) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                // Derive owner/repo/pr from existing metadata.json — unambiguous and avoids
                // underscore-splitting heuristics that break for repos with underscores in the name.
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
                    PrintMetadata(metadata);
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

    // ── gauntletci corpus list ───────────────────────────────────────────────

    private static Command CreateList()
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

            // Expand comma-separated tags
            var expandedTags = tags
                .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();

            var (db, store, _) = await BuildPipeline(dbPath, fixtures, ct);
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
                    var jsonOpts = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new JsonStringEnumConverter() },
                    };
                    Console.WriteLine(JsonSerializer.Serialize(results, jsonOpts));
                }
                else
                {
                    PrintFixtureTable(results);
                }
            }
        });

        return cmd;
    }

    private static void PrintFixtureTable(IReadOnlyList<FixtureMetadata> fixtures)
    {
        const int colFixtureId = 36;
        const int colTier      = 10;
        const int colSize      = 8;
        const int colLanguage  = 12;
        const int colRules     = 30;

        var header = $"{"FixtureId",-colFixtureId}  {"Tier",-colTier}  {"Size",-colSize}  {"Language",-colLanguage}  {"Rules",-colRules}  Tags";
        var sep    = new string('-', header.Length + 4);

        Console.WriteLine(sep);
        Console.WriteLine(header);
        Console.WriteLine(sep);

        foreach (var m in fixtures)
        {
            var rules = string.Join(",", m.RuleIds);
            var tagsStr = string.Join(",", m.Tags);
            Console.WriteLine(
                $"{m.FixtureId,-colFixtureId}  {m.Tier,-colTier}  {m.PrSizeBucket,-colSize}  {m.Language,-colLanguage}  {rules,-colRules}  {tagsStr}");
        }

        Console.WriteLine(sep);
        Console.WriteLine($"[corpus] {fixtures.Count} fixture(s)");
    }

    // ── gauntletci corpus discover --provider gh-search|gh-archive ───────────

    private static Command CreateDiscover()
    {
        var providerOpt    = new Option<string>("--provider",     "Discovery provider: gh-search or gh-archive") { IsRequired = true };
        var limitOpt       = new Option<int>   ("--limit",        () => 100,           "Maximum candidates to fetch");
        var languageOpt    = new Option<string?>("--language",    "Filter by programming language (e.g. cs, python)");
        var minStarsOpt    = new Option<int>   ("--min-stars",    () => 0,             "Minimum stars on the repository");
        var minCommentsOpt = new Option<int>   ("--min-comments", () => 0,             "Minimum review comment count");
        var startDateOpt   = new Option<DateTime?>("--start-date","Filter by merge/event date (inclusive, UTC)");
        var dbOpt          = new Option<string>("--db",           () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt    = new Option<string>("--fixtures",     () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("discover", "Discover pull request candidates and persist them to the corpus database");
        cmd.AddOption(providerOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(languageOpt);
        cmd.AddOption(minStarsOpt);
        cmd.AddOption(minCommentsOpt);
        cmd.AddOption(startDateOpt);
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
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var ct           = ctx.GetCancellationToken();

            IDiscoveryProvider provider;

            if (providerName.Equals("gh-search", StringComparison.OrdinalIgnoreCase))
            {
                var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (string.IsNullOrEmpty(token))
                {
                    Console.Error.WriteLine("[corpus] Error: GITHUB_TOKEN environment variable is required for gh-search provider.");
                    ctx.ExitCode = 1;
                    return;
                }
                provider = new GitHubSearchDiscoveryProvider(token);
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

            var languages = string.IsNullOrWhiteSpace(language)
                ? Array.Empty<string>()
                : new[] { language };

            var query = new DiscoveryQuery
            {
                Languages          = languages,
                MinStars           = minStars,
                MinReviewComments  = minComments,
                StartDateUtc       = startDate,
                MaxCandidates      = limit,
            };

            Console.WriteLine($"[corpus] Discovering candidates via {provider.GetProviderName()} (limit={limit}) …");

            var candidates = await provider.SearchCandidatesAsync(query, ct);

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                int inserted = 0;

                foreach (var c in candidates)
                {
                    var id = $"{c.RepoOwner}/{c.RepoName}#{c.PullRequestNumber}";
                    using var insertCmd = db.Connection.CreateCommand();
                    insertCmd.CommandText = """
                        INSERT OR IGNORE INTO candidates
                            (id, source, repo_owner, repo_name, pr_number, url, language,
                             created_at_utc, updated_at_utc, review_comment_count, candidate_reason)
                        VALUES
                            ($id, $source, $owner, $repo, $prNumber, $url, $language,
                             $createdAt, $updatedAt, $reviewComments, $reason)
                        """;
                    insertCmd.Parameters.AddWithValue("$id",             id);
                    insertCmd.Parameters.AddWithValue("$source",         c.Source);
                    insertCmd.Parameters.AddWithValue("$owner",          c.RepoOwner);
                    insertCmd.Parameters.AddWithValue("$repo",           c.RepoName);
                    insertCmd.Parameters.AddWithValue("$prNumber",       c.PullRequestNumber);
                    insertCmd.Parameters.AddWithValue("$url",            c.Url);
                    insertCmd.Parameters.AddWithValue("$language",       (object?)c.Language ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("$createdAt",      c.CreatedAtUtc == default ? DBNull.Value : (object)c.CreatedAtUtc.ToString("O"));
                    insertCmd.Parameters.AddWithValue("$updatedAt",      c.UpdatedAtUtc == default ? DBNull.Value : (object)c.UpdatedAtUtc.ToString("O"));
                    insertCmd.Parameters.AddWithValue("$reviewComments", c.ReviewCommentCount);
                    insertCmd.Parameters.AddWithValue("$reason",         (object?)c.CandidateReason ?? DBNull.Value);

                    var rows = await insertCmd.ExecuteNonQueryAsync(ct);
                    if (rows > 0) inserted++;
                }

                var skipped = candidates.Count - inserted;
                Console.WriteLine($"[corpus] Discovered {candidates.Count} candidates ({inserted} new, {skipped} already known)");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus batch-hydrate ──────────────────────────────────────

    private static Command CreateBatchHydrate()
    {
        var limitOpt    = new Option<int>   ("--limit",    () => 10,          "Maximum number of candidates to hydrate");
        var tierOpt     = new Option<string>("--tier",     () => "discovery", "Target tier (gold|silver|discovery)");
        var dryRunOpt   = new Option<bool>  ("--dry-run",  () => false,       "Print what would be processed without hydrating");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("batch-hydrate", "Bulk hydrate pending candidates from the corpus database");
        cmd.AddOption(limitOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var limit    = ctx.ParseResult.GetValueForOption(limitOpt);
            var tierStr  = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var dryRun   = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct       = ctx.GetCancellationToken();

            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[corpus] Error: GITHUB_TOKEN environment variable is not set.");
                ctx.ExitCode = 1;
                return;
            }

            var (db, _, pipeline) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                var candidates = await QueryPendingCandidatesAsync(db.Connection, limit, ct);

                if (candidates.Count == 0)
                {
                    Console.WriteLine("[corpus] No pending candidates found.");
                    return;
                }

                int success = 0;
                int total   = candidates.Count;

                foreach (var (owner, repo, prNumber) in candidates)
                {
                    var url = $"https://github.com/{owner}/{repo}/pull/{prNumber}";

                    if (dryRun)
                    {
                        Console.WriteLine($"[corpus] [dry-run] Would hydrate {owner}/{repo}#{prNumber}");
                        success++;
                        continue;
                    }

                    var fixtureId = GauntletCI.Corpus.Storage.FixtureIdHelper.Build(owner, repo, prNumber);
                    Console.WriteLine($"[corpus] Hydrating {owner}/{repo}#{prNumber} → {fixtureId}");

                    try
                    {
                        using var hydrator = GitHubRestHydrator.CreateDefault(fixtures);
                        var hydrated = await hydrator.HydrateFromUrlAsync(url, ct);
                        await pipeline.NormalizeAsync(hydrated, source: "batch", ct: ct);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[corpus] Error hydrating {owner}/{repo}#{prNumber}: {ex.Message}");
                    }
                }

                Console.WriteLine($"[corpus] Batch complete: {success}/{total} fixtures hydrated");
            }
        });

        return cmd;
    }

    private static async Task<IReadOnlyList<(string Owner, string Repo, int PrNumber)>>
        QueryPendingCandidatesAsync(SqliteConnection conn, int limit, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.repo_owner, c.repo_name, c.pr_number
            FROM candidates c
            LEFT JOIN fixtures f ON f.fixture_id = (c.repo_owner || '_' || c.repo_name || '_pr' || c.pr_number)
            WHERE f.fixture_id IS NULL
            ORDER BY c.discovered_at_utc DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<(string, string, int)>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));

        return results;
    }

    // ── gauntletci corpus run --fixture <id> ─────────────────────────────────

    private static Command CreateRun()
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

            var (db, store, _) = await BuildPipeline(dbPath, fixtures, ct);
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

                    var fixturePath = FixtureIdHelper.GetFixturePath(fixtures, metadata.Tier, fixtureId);
                    var diffPath    = Path.Combine(fixturePath, "diff.patch");

                    if (!File.Exists(diffPath))
                    {
                        Console.Error.WriteLine($"[corpus] diff.patch not found at {diffPath}");
                        ctx.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine($"[corpus] Running GCI rules against {fixtureId}");

                    var diffText = await File.ReadAllTextAsync(diffPath, ct);
                    var runner   = new RuleCorpusRunner(store, db);
                    var findings = await runner.RunAsync(fixtureId, diffText, ct);

                    int high   = findings.Count(f => f.ActualConfidence >= 1.0);
                    int medium = findings.Count(f => f.ActualConfidence is >= 0.5 and < 1.0);
                    int low    = findings.Count(f => f.ActualConfidence < 0.5);

                    Console.WriteLine($"[corpus] Run ID  : {runner.LastRunId}");
                    Console.WriteLine($"[corpus] Findings: {findings.Count} ({high} High, {medium} Medium, {low} Low)");
                    Console.WriteLine($"[corpus] Saved to: {fixturePath}");
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

    // ── gauntletci corpus run-all [--tier ...] ───────────────────────────────

    private static Command CreateRunAll()
    {
        var tierOpt     = new Option<string?>("--tier",     "Filter by tier (gold|silver|discovery)");
        var dbOpt       = new Option<string> ("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string> ("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("run-all", "Run GCI rules against all (or filtered) corpus fixtures");
        cmd.AddOption(tierOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr  = ctx.ParseResult.GetValueForOption(tierOpt);
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

            var (db, store, _) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
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

                foreach (var metadata in allFixtures)
                {
                    var fixturePath = FixtureIdHelper.GetFixturePath(fixtures, metadata.Tier, metadata.FixtureId);
                    var diffPath    = Path.Combine(fixturePath, "diff.patch");

                    if (!File.Exists(diffPath))
                    {
                        Console.WriteLine($"[corpus] SKIP {metadata.FixtureId} — diff.patch not found");
                        failed++;
                        continue;
                    }

                    try
                    {
                        var diffText = await File.ReadAllTextAsync(diffPath, ct);
                        var runner   = new RuleCorpusRunner(store, db);
                        var findings = await runner.RunAsync(metadata.FixtureId, diffText, ct);

                        totalFindings += findings.Count;
                        completed++;

                        Console.WriteLine($"[corpus] OK  {metadata.FixtureId,-40} {findings.Count,3} finding(s)");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[corpus] ERR {metadata.FixtureId}: {ex.Message}");
                        failed++;
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"[corpus] Run-all complete: {completed} OK, {failed} skipped/failed, {totalFindings} total findings");
            }
        });

        return cmd;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static async Task<(CorpusDb Db, FixtureFolderStore Store, NormalizationPipeline Pipeline)>
        BuildPipeline(string dbPath, string fixturesPath, CancellationToken ct)
    {
        var db       = new CorpusDb(dbPath);
        await db.InitializeAsync(ct);
        var store    = new FixtureFolderStore(db, fixturesPath);
        var pipeline = new NormalizationPipeline(store);
        return (db, store, pipeline);
    }

    private static void PrintMetadata(GauntletCI.Corpus.Models.FixtureMetadata m)
    {
        Console.WriteLine($"[corpus] Fixture : {m.FixtureId}");
        Console.WriteLine($"[corpus] Tier    : {m.Tier}");
        Console.WriteLine($"[corpus] Size    : {m.PrSizeBucket} ({m.FilesChanged} files)");
        Console.WriteLine($"[corpus] Language: {m.Language}");
        Console.WriteLine($"[corpus] Tags    : {string.Join(", ", m.Tags)}");
        Console.WriteLine($"[corpus] Next    : gauntletci corpus normalize --fixture {m.FixtureId}");
    }
}
