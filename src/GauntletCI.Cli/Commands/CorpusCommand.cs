// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Discovery;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Scoring;
using GauntletCI.Corpus.Storage;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Cli.Commands;

public static class CorpusCommand
{
    public static Command Create()
    {
        var corpus = new Command("corpus", """
            Manage the GauntletCI fixture corpus.

            Typical workflow:
              1. corpus discover --provider gh-search --repo-allowlist owner/repo
              2. corpus batch-hydrate --limit 50
              3. corpus label-all --tier discovery
              4. corpus run-all --tier discovery
              5. corpus score
              6. corpus report
            """);
        corpus.AddCommand(CreateAddPr());
        corpus.AddCommand(CreateNormalize());
        corpus.AddCommand(CreateList());
        corpus.AddCommand(CreateBatchHydrate());
        corpus.AddCommand(CreateDiscover());
        corpus.AddCommand(CreateRun());
        corpus.AddCommand(CreateRunAll());
        corpus.AddCommand(CreateScore());
        corpus.AddCommand(CreateReport());
        corpus.AddCommand(CreateLabel());
        corpus.AddCommand(CreateLabelAll());
        corpus.AddCommand(CreateResetStats());
        corpus.AddCommand(CreatePurge());

        var issues = new Command("issues", "GitHub Issues corpus operations");
        issues.AddCommand(CreateIssueSearch());
        corpus.AddCommand(issues);

        var maintainers = new Command("maintainers", "Expert maintainer knowledge acquisition");
        maintainers.AddCommand(CreateMaintainersFetch());
        corpus.AddCommand(maintainers);

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

                    var fixtureId = GauntletCI.Corpus.Storage.FixtureIdHelper.Build(
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
        var perRepoLimitOpt  = new Option<int>   ("--per-repo-limit", () => 0,              "Max candidates per repo when using allowlist (0 = unlimited, shared across --limit)");
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
            var ct             = ctx.GetCancellationToken();

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
                if (repoAllowlist.Length == 0)
                {
                    Console.Error.WriteLine("[corpus] Error: --repo-allowlist is required for gh-search. Pass one or more owner/repo values.");
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
                EndDateUtc         = endDate,
                MaxCandidates      = limit,
                PerRepoLimit       = perRepoLimit,
                RepoBlockList      = repoBlocklist,
                RepoAllowList      = repoAllowlist,
            };

            Console.WriteLine($"[corpus] Discovering candidates via {provider.GetProviderName()} (limit={limit}{(perRepoLimit > 0 ? $", per-repo={perRepoLimit}" : "")}) …");

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

            if (!Enum.TryParse<GauntletCI.Corpus.Models.FixtureTier>(tierStr, ignoreCase: true, out var tier))
            {
                Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                ctx.ExitCode = 1;
                return;
            }

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
                        await pipeline.NormalizeAsync(hydrated, source: "batch", tier: tier, ct: ct);

                        // Persist actual review comment count back to candidates so purge can use it
                        var candidateId = $"{owner}/{repo}#{prNumber}";
                        using var updateCmd = db.Connection.CreateCommand();
                        updateCmd.CommandText = "UPDATE candidates SET review_comment_count = $count WHERE id = $id";
                        updateCmd.Parameters.AddWithValue("$count", hydrated.ReviewComments.Count);
                        updateCmd.Parameters.AddWithValue("$id", candidateId);
                        await updateCmd.ExecuteNonQueryAsync(ct);

                        using var enricher = IssueEnricher.CreateDefault();
                        var linked = await enricher.EnrichAsync(
                            db.Connection, fixtureId, owner, repo, hydrated.Body, ct);
                        if (linked > 0) Console.WriteLine($"[corpus] Linked {linked} issue(s) to fixture");

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
            LEFT JOIN fixtures f ON f.fixture_id = (
                replace(replace(replace(lower(c.repo_owner), '/', '_'), '\', '_'), ' ', '-') || '_' ||
                replace(replace(replace(lower(c.repo_name),  '/', '_'), '\', '_'), ' ', '-') || '_pr' || c.pr_number
            )
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

    // ── gauntletci corpus score ───────────────────────────────────────────────

    private static Command CreateScore()
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

            var (db, store, _) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                var aggregator = new ScoreAggregator(store, db);
                var scorecards = await aggregator.ScoreAsync(ruleId, tier, ct);

                if (scorecards.Count == 0)
                {
                    Console.WriteLine("[corpus] No scorecards — run 'corpus run-all' first to generate actual.json files.");
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
        });

        return cmd;
    }

    // ── gauntletci corpus report ──────────────────────────────────────────────

    private static Command CreateReport()
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

            var (db, store, _) = await BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                var aggregator = new ScoreAggregator(store, db);
                var exporter   = new MarkdownReportExporter(aggregator);
                var markdown   = await exporter.ExportMarkdownAsync(ct);

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(outputPath, markdown, ct);
                Console.WriteLine($"[corpus] Report written to {outputPath}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus label --fixture <id> ────────────────────────────────

    private static Command CreateLabel()
    {
        var fixtureOpt   = new Option<string>("--fixture",   "Fixture ID to label") { IsRequired = true };
        var overwriteOpt = new Option<bool>  ("--overwrite", () => false, "Overwrite existing HumanReview/Seed labels with heuristic labels");
        var dbOpt        = new Option<string>("--db",        () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt  = new Option<string>("--fixtures",  () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("label", "Apply silver heuristic labels to a single corpus fixture");
        cmd.AddOption(fixtureOpt);
        cmd.AddOption(overwriteOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var fixtureId = ctx.ParseResult.GetValueForOption(fixtureOpt)!;
            var overwrite = ctx.ParseResult.GetValueForOption(overwriteOpt);
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

                    var diffText = await File.ReadAllTextAsync(diffPath, ct);
                    var engine   = new SilverLabelEngine(store);

                    var labelsWritten = await engine.ApplyToFixtureAsync(fixtureId, diffText, overwrite, ct);

                    Console.WriteLine($"[corpus] Labeled {fixtureId}: {labelsWritten} label(s) written");
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

    // ── gauntletci corpus label-all ───────────────────────────────────────────

    private static Command CreateLabelAll()
    {
        var tierOpt      = new Option<string>("--tier",      () => "discovery", "Fixture tier to process (gold|silver|discovery)");
        var overwriteOpt = new Option<bool>  ("--overwrite", () => false,       "Overwrite existing HumanReview/Seed labels with heuristic labels");
        var llmLabelOpt  = new Option<bool>  ("--llm-label", () => false,       "Enable LLM-based Tier 3 labeling (requires ANTHROPIC_API_KEY env var)");
        var dbOpt        = new Option<string>("--db",        () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt  = new Option<string>("--fixtures",  () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("label-all", "Apply silver heuristic labels to all fixtures in a tier");
        cmd.AddOption(tierOpt);
        cmd.AddOption(overwriteOpt);
        cmd.AddOption(llmLabelOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr   = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var overwrite = ctx.ParseResult.GetValueForOption(overwriteOpt);
            var llmLabel  = ctx.ParseResult.GetValueForOption(llmLabelOpt);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var tier))
            {
                Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                ctx.ExitCode = 1;
                return;
            }

            ILlmLabeler llmLabeler = new NullLlmLabeler();
            if (llmLabel)
            {
                var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                    llmLabeler = new AnthropicLlmLabeler(apiKey);
                else
                    Console.WriteLine("[corpus] ANTHROPIC_API_KEY not set — LLM labeling disabled");
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

                int labeled = 0;
                int skipped = 0;
                int totalLabels = 0;
                var engine = new SilverLabelEngine(store, llmLabeler);

                foreach (var metadata in allFixtures)
                {
                    var fixturePath = FixtureIdHelper.GetFixturePath(fixtures, metadata.Tier, metadata.FixtureId);
                    var diffPath    = Path.Combine(fixturePath, "diff.patch");

                    if (!File.Exists(diffPath))
                    {
                        Console.WriteLine($"[corpus] SKIP {metadata.FixtureId,-40} — diff.patch not found");
                        skipped++;
                        continue;
                    }

                    try
                    {
                        var diffText      = await File.ReadAllTextAsync(diffPath, ct);
                        var labelsWritten = await engine.ApplyToFixtureAsync(metadata.FixtureId, diffText, overwrite, ct);

                        totalLabels += labelsWritten;
                        labeled++;

                        Console.WriteLine($"[corpus] OK   {metadata.FixtureId,-40} {labelsWritten,3} label(s)");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[corpus] ERR  {metadata.FixtureId}: {ex.Message}");
                        skipped++;
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"[corpus] label-all complete: {labeled} labeled, {skipped} skipped, {totalLabels} total labels written");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus reset-stats ────────────────────────────────────────

    private static Command CreateResetStats()
    {
        var dbOpt      = new Option<string>("--db",      () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var ruleOpt    = new Option<string?>("--rule",   "Limit reset to a specific rule ID (e.g. GCI0001)");
        var confirmOpt = new Option<bool>("--confirm",   "Required: confirm you want to delete run and scoring data");

        var cmd = new Command("reset-stats", "Delete rule run, scoring, and evaluation data from the corpus database");
        cmd.AddOption(dbOpt);
        cmd.AddOption(ruleOpt);
        cmd.AddOption(confirmOpt);

        cmd.SetHandler((ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var ruleId  = ctx.ParseResult.GetValueForOption(ruleOpt);
            var confirm = ctx.ParseResult.GetValueForOption(confirmOpt);

            if (!confirm)
            {
                Console.Error.WriteLine("[corpus] reset-stats requires --confirm to prevent accidental data loss.");
                Console.Error.WriteLine("[corpus] Re-run with: gauntletci corpus reset-stats --confirm");
                ctx.ExitCode = 1;
                return;
            }

            if (!File.Exists(dbPath))
            {
                Console.Error.WriteLine($"[corpus] Database not found: {dbPath}");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                string ruleFilter     = ruleId is not null ? " WHERE rule_id = @rule" : "";
                string ruleFilterJoin = ruleId is not null ? " WHERE af.rule_id = @rule" : "";

                using var tx = connection.BeginTransaction();

                void Exec(string sql)
                {
                    using var cmd2 = connection.CreateCommand();
                    cmd2.Transaction = tx;
                    cmd2.CommandText = sql;
                    if (ruleId is not null) cmd2.Parameters.AddWithValue("@rule", ruleId);
                    cmd2.ExecuteNonQuery();
                }

                // Delete scoring and evaluation data (scoped to rule if provided).
                Exec($"DELETE FROM aggregates{ruleFilter}");
                Exec($"DELETE FROM evaluations{ruleFilter}");
                Exec($"DELETE FROM actual_findings{(ruleId is not null ? " WHERE rule_id = @rule" : "")}");

                // For rule_runs, only delete if scoped (a run covers all rules) or if no filter.
                if (ruleId is null)
                    Exec("DELETE FROM rule_runs");

                tx.Commit();

                var scope = ruleId is not null ? $" for rule {ruleId}" : "";
                Console.WriteLine($"[corpus] reset-stats complete{scope}: aggregates, evaluations, and actual_findings cleared.");
                if (ruleId is null)
                    Console.WriteLine("[corpus] rule_runs cleared.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[corpus] Error during reset-stats: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── gauntletci corpus issues search ──────────────────────────────────────

    private static Command CreateIssueSearch()
    {
        var languageOpt = new Option<string>("--language", () => "cs",           "Programming language filter (e.g. cs, python)");
        var limitOpt    = new Option<int>   ("--limit",    () => 50,             "Maximum candidates to fetch");
        var labelsOpt   = new Option<string>("--labels",   () => "bug,security", "Comma-separated GitHub issue labels to search for");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");

        var cmd = new Command("search", "Search for corpus candidates via closed GitHub issues");
        cmd.AddOption(languageOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(labelsOpt);
        cmd.AddOption(dbOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var language = ctx.ParseResult.GetValueForOption(languageOpt)!;
            var limit    = ctx.ParseResult.GetValueForOption(limitOpt);
            var labels   = ctx.ParseResult.GetValueForOption(labelsOpt)!;
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var ct       = ctx.GetCancellationToken();

            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[corpus] Error: GITHUB_TOKEN environment variable is required for issue search.");
                ctx.ExitCode = 1;
                return;
            }

            var query = new DiscoveryQuery
            {
                Languages     = new[] { language },
                MaxCandidates = limit,
            };

            Console.WriteLine($"[corpus/issues] Searching for closed issues with labels: {labels}");

            using var provider = new GitHubIssueDiscoveryProvider(token, labels);
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
                Console.WriteLine($"[corpus/issues] Found {candidates.Count} candidates ({inserted} new, {skipped} already known)");
            }
        });

        return cmd;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    // ── gauntletci corpus purge ───────────────────────────────────────────────

    private static Command CreatePurge()
    {
        var languageOpt              = new Option<string>("--language",              () => "C#",  "Remove fixtures whose inferred language doesn't match this value. Pass empty to skip language filter.");
        var requireReviewCommentsOpt = new Option<bool>("--require-review-comments", () => false, "Remove fixtures that have no inline review comments");
        var repoBlocklistOpt         = new Option<string[]>("--repo-blocklist",      "Remove fixtures from these owner/repo names (e.g. 'Goob-Station/Goob-Station')") { AllowMultipleArgumentsPerToken = false, Arity = ArgumentArity.ZeroOrMore };
        var dryRunOpt                = new Option<bool>  ("--dry-run",              () => false, "Print what would be purged without making changes");
        var dbOpt                    = new Option<string>("--db",                   () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt              = new Option<string>("--fixtures",             () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("purge", "Remove low-quality fixtures from the corpus (language mismatch, no review comments, blocklisted repos)");
        cmd.AddOption(languageOpt);
        cmd.AddOption(requireReviewCommentsOpt);
        cmd.AddOption(repoBlocklistOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var language              = ctx.ParseResult.GetValueForOption(languageOpt)!;
            var requireReviewComments = ctx.ParseResult.GetValueForOption(requireReviewCommentsOpt);
            var repoBlocklist         = ctx.ParseResult.GetValueForOption(repoBlocklistOpt) ?? [];
            var dryRun                = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var dbPath                = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesRoot          = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct                    = ctx.GetCancellationToken();

            var (db, _, _) = await BuildPipeline(dbPath, fixturesRoot, ct);
            using (db)
            {
                // Build WHERE predicate
                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(language))
                    conditions.Add("(f.language IS NULL OR LOWER(f.language) != LOWER($lang))");
                if (requireReviewComments)
                    conditions.Add("f.has_review_comments = 0");
                if (repoBlocklist.Length > 0)
                    conditions.Add("f.repo IN (" + string.Join(",", repoBlocklist.Select((_, i) => $"$blk{i}")) + ")");

                if (conditions.Count == 0)
                {
                    Console.WriteLine("[corpus] purge: no filters specified — nothing to do.");
                    return;
                }

                var where = string.Join(" OR ", conditions);

                // Collect fixtures to purge
                using var selectCmd = db.Connection.CreateCommand();
                selectCmd.CommandText = $"""
                    SELECT f.fixture_id, f.path, f.repo, f.pr_number, f.language, f.has_review_comments
                    FROM fixtures f
                    WHERE {where}
                    """;
                selectCmd.Parameters.AddWithValue("$lang", language);
                for (int i = 0; i < repoBlocklist.Length; i++)
                    selectCmd.Parameters.AddWithValue($"$blk{i}", repoBlocklist[i]);

                var toPurge = new List<(string FixtureId, string? Path, string Repo, int PrNumber)>();
                using (var reader = await selectCmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var fid  = reader.GetString(0);
                        var path = reader.IsDBNull(1) ? null : reader.GetString(1);
                        var repo = reader.GetString(2);
                        var prn  = reader.GetInt32(3);
                        var lang = reader.IsDBNull(4) ? "(none)" : reader.GetString(4);
                        var hasRc = reader.GetInt32(5) == 1;
                        var blocklisted = repoBlocklist.Length > 0 && repoBlocklist.Contains(repo, StringComparer.OrdinalIgnoreCase);
                        var reason = blocklisted ? "blocklisted" : (!hasRc ? "no-review-comments" : $"lang={lang}");
                        Console.WriteLine($"[corpus] purge: {fid}  reason={reason}");
                        toPurge.Add((fid, path, repo, prn));
                    }
                }

                if (toPurge.Count == 0)
                {
                    Console.WriteLine("[corpus] purge: no fixtures matched the filter — corpus is clean.");
                    return;
                }

                if (dryRun)
                {
                    Console.WriteLine($"[corpus] purge: [dry-run] would remove {toPurge.Count} fixture(s).");
                    return;
                }

                using var tx = db.Connection.BeginTransaction();
                int removed = 0;
                foreach (var (fixtureId, path, repo, prNumber) in toPurge)
                {
                    var candidateId = $"{repo}#{prNumber}";

                    void Exec(string sql, string param, object val)
                    {
                        using var c = db.Connection.CreateCommand();
                        c.Transaction = tx;
                        c.CommandText = sql;
                        c.Parameters.AddWithValue(param, val);
                        c.ExecuteNonQuery();
                    }

                    Exec("DELETE FROM actual_findings  WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM expected_findings WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM evaluations       WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM rule_runs         WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM fixture_issues    WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM fixtures          WHERE fixture_id = $fid", "$fid", fixtureId);
                    Exec("DELETE FROM hydrations        WHERE candidate_id = $cid", "$cid", candidateId);
                    Exec("DELETE FROM candidates        WHERE id = $cid",           "$cid", candidateId);

                    // Remove fixture directory from disk
                    if (path is not null && Directory.Exists(path))
                    {
                        try { Directory.Delete(path, recursive: true); }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[corpus] purge: warning — could not delete {path}: {ex.Message}");
                        }
                    }

                    removed++;
                }
                tx.Commit();

                Console.WriteLine($"[corpus] purge: removed {removed} fixture(s) from DB and disk.");
            }
        });

        return cmd;
    }

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

    // ── gauntletci corpus maintainers fetch ──────────────────────────────────

    private static Command CreateMaintainersFetch()
    {
        var outputOpt = new Option<string>("--output", () => "./data/maintainer-records.ndjson",
            "Output path for NDJSON records");
        var maxOpt    = new Option<int>("--max-per-label", () => 100,
            "Max search results per label per repo");
        var reposOpt  = new Option<string[]>("--repo",
            "Additional repos to fetch (format: owner/repo). Can specify multiple times.")
            { AllowMultipleArgumentsPerToken = false, Arity = ArgumentArity.ZeroOrMore };

        var cmd = new Command("fetch", "Fetch high-signal PRs/issues from top OSS contributors");
        cmd.AddOption(outputOpt);
        cmd.AddOption(maxOpt);
        cmd.AddOption(reposOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var output      = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var max         = ctx.ParseResult.GetValueForOption(maxOpt);
            var extraRepos  = ctx.ParseResult.GetValueForOption(reposOpt) ?? [];
            var ct          = ctx.GetCancellationToken();

            var targets = GauntletCI.Corpus.MaintainerFetcher.MaintainerTarget.Defaults.ToList();
            foreach (var r in extraRepos)
            {
                var parts = r.Split('/', 2);
                if (parts.Length == 2)
                    targets.Add(new GauntletCI.Corpus.MaintainerFetcher.MaintainerTarget(
                        parts[0], parts[1], ["performance", "design-discussion"]));
            }

            Console.WriteLine($"[maintainers] Fetching from {targets.Count} repos, max {max} per label…");

            using var fetcher = GauntletCI.Corpus.MaintainerFetcher.MaintainerFetcher.CreateDefault();
            var records = await fetcher.FetchAsync([.. targets], max, ct);

            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var jsonOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            };

            await using var writer = new StreamWriter(output, append: false, System.Text.Encoding.UTF8);
            foreach (var rec in records)
                await writer.WriteLineAsync(JsonSerializer.Serialize(rec, jsonOpts));

            Console.WriteLine($"[maintainers] Wrote {records.Count} records to {output}");
        });

        return cmd;
    }
}
