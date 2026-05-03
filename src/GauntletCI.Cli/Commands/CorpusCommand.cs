// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Discovery;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Scoring;
using GauntletCI.Corpus.Storage;
using GauntletCI.Core.Configuration;
using GauntletCI.Cli.Commands.Factories;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Cli.Commands;

public static class CorpusCommand
{
    public static Command Create()
    {
        // Initialize factory implementations for DI
        ICorpusOperationsFactory opsFactory = new CorpusOperationsFactoryImpl();
        ICorpusAnalysisFactory analysisFactory = new CorpusAnalysisFactoryImpl();
        ICorpusLabelingFactory labelingFactory = new CorpusLabelingFactoryImpl();
        ICorpusUtilityFactory utilityFactory = new CorpusUtilityFactoryImpl();

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
        
        // Operations commands
        corpus.AddCommand(opsFactory.CreateAddPr());
        corpus.AddCommand(opsFactory.CreateNormalize());
        corpus.AddCommand(opsFactory.CreateList());
        corpus.AddCommand(opsFactory.CreateShow());
        corpus.AddCommand(opsFactory.CreateStatus());
        corpus.AddCommand(opsFactory.CreateBatchHydrate());
        
        // Analysis commands
        corpus.AddCommand(analysisFactory.CreateDiscover());
        corpus.AddCommand(analysisFactory.CreateRun());
        corpus.AddCommand(analysisFactory.CreateRunAll());
        corpus.AddCommand(analysisFactory.CreateScore());
        corpus.AddCommand(analysisFactory.CreateReport());
        
        // Labeling commands
        corpus.AddCommand(labelingFactory.CreateLabel());
        corpus.AddCommand(labelingFactory.CreateLabelAll());
        corpus.AddCommand(labelingFactory.CreateResetStats());
        
        // Utility commands
        corpus.AddCommand(utilityFactory.CreatePurge());
        corpus.AddCommand(utilityFactory.CreateErrors());
        corpus.AddCommand(utilityFactory.CreateRejectedRepos());
        corpus.AddCommand(utilityFactory.CreateDoctor());

        var issues = new Command("issues", "GitHub Issues corpus operations");
        issues.AddCommand(CreateIssueSearch());
        corpus.AddCommand(issues);

        var maintainers = new Command("maintainers", "Expert maintainer knowledge acquisition");
        maintainers.AddCommand(CreateMaintainersFetch());
        corpus.AddCommand(maintainers);

        corpus.AddCommand(CreateFetchDiffs());

        var sonarcloud = new Command("sonarcloud", "SonarCloud external validation operations");
        sonarcloud.AddCommand(CreateSonarCloudEnrich());
        corpus.AddCommand(sonarcloud);

        var codescanning = new Command("codescanning", "GitHub Code Scanning (CodeQL) external validation operations");
        codescanning.AddCommand(CreateCodeScanningEnrich());
        codescanning.AddCommand(CreateCodeScanningCheckRepos());
        corpus.AddCommand(codescanning);

        var dependabot = new Command("dependabot", "Dependabot Tier 1 oracle operations");
        dependabot.AddCommand(CreateDependabotEnrich());
        dependabot.AddCommand(CreateDependabotDiscover());
        corpus.AddCommand(dependabot);

        var socialSignal = new Command("social-signal", "PR review social-signal Tier 2 oracle operations");
        socialSignal.AddCommand(CreateSocialSignalEnrich());
        corpus.AddCommand(socialSignal);

        var compositeLabel = new Command("composite-label", "Composite ground-truth labeling operations");
        compositeLabel.AddCommand(CreateCompositeLabelApply());
        corpus.AddCommand(compositeLabel);

        var semgrep = new Command("semgrep", "Semgrep scanner enrichment");
        semgrep.AddCommand(CreateSemgrepEnrich());
        corpus.AddCommand(semgrep);

        var structural = new Command("structural", "Structural/file-churn enrichment");
        structural.AddCommand(CreateStructuralEnrich());
        corpus.AddCommand(structural);

        var nugetAdvisory = new Command("nuget-advisory", "NuGet advisory GHSA vulnerability enrichment");
        nugetAdvisory.AddCommand(CreateNuGetAdvisoryEnrich());
        corpus.AddCommand(nugetAdvisory);

        var fileChurn = new Command("file-churn", "90-day per-file commit churn hotspot enrichment");
        fileChurn.AddCommand(CreateFileChurnEnrich());
        corpus.AddCommand(fileChurn);

        var reviewNlp = new Command("review-nlp", "Review comment NLP taxonomy enrichment");
        reviewNlp.AddCommand(CreateReviewNlpEnrich());
        corpus.AddCommand(reviewNlp);

        var testCoverage = new Command("test-coverage", "Pure-diff test coverage gap enrichment");
        testCoverage.AddCommand(CreateTestCoverageEnrich());
        corpus.AddCommand(testCoverage);

        var diffEntropy = new Command("diff-entropy", "Pure-diff Kamei entropy signal enrichment");
        diffEntropy.AddCommand(CreateDiffEntropyEnrich());
        corpus.AddCommand(diffEntropy);

        var efMigration = new Command("ef-migration", "Pure-diff EF Core migration and SQL DDL detection");
        efMigration.AddCommand(CreateEfMigrationEnrich());
        corpus.AddCommand(efMigration);

        var prDescription = new Command("pr-description", "PR description quality enrichment");
        prDescription.AddCommand(CreatePrDescriptionEnrich());
        corpus.AddCommand(prDescription);

        var authorExperience = new Command("author-experience", "PR author experience and first-contributor enrichment");
        authorExperience.AddCommand(CreateAuthorExperienceEnrich());
        corpus.AddCommand(authorExperience);

        return corpus;
    }


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

            var token = GitHubTokenResolver.Resolve();
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[corpus] Error: no GitHub token found. Set GITHUB_TOKEN or run 'gh auth login'.");
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
                var rejectedRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var rejectedCmd = db.Connection.CreateCommand())
                {
                    rejectedCmd.CommandText = "SELECT repo_owner || '/' || repo_name FROM repo_rejections";
                    using var rejectedReader = await rejectedCmd.ExecuteReaderAsync(ct);
                    while (await rejectedReader.ReadAsync(ct))
                        rejectedRepos.Add(rejectedReader.GetString(0));
                }

                int inserted = 0;
                int rejected = 0;
                foreach (var c in candidates)
                {
                    var repoSpec = $"{c.RepoOwner}/{c.RepoName}";
                    if (rejectedRepos.Contains(repoSpec))
                    {
                        rejected++;
                        continue;
                    }

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

                var skipped = candidates.Count - inserted - rejected;
                Console.WriteLine($"[corpus/issues] Found {candidates.Count} candidates ({inserted} new, {skipped} already known, {rejected} rejected repo)");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus doctor ──────────────────────────────────────────────

    private static Command CreateDoctor()
    {
        var dbOpt    = new Option<string>("--db",    () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var tokenOpt = new Option<string?>("--token", "GitHub token (overrides GITHUB_TOKEN env var)");

        var cmd = new Command("doctor", "Check GitHub API connectivity, rate limits, and recent pipeline errors");
        cmd.AddOption(dbOpt);
        cmd.AddOption(tokenOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var token  = ctx.ParseResult.GetValueForOption(tokenOpt)
                         ?? GitHubTokenResolver.Resolve();
            var ct     = ctx.GetCancellationToken();

            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[corpus] Error: GitHub token is required. Set GITHUB_TOKEN or pass --token.");
                ctx.ExitCode = 1;
                return;
            }

            Console.WriteLine("[corpus] Checking GitHub connectivity...");

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            System.Net.Http.HttpResponseMessage rateLimitResponse;
            try
            {
                rateLimitResponse = await http.GetAsync("https://api.github.com/rate_limit", ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[corpus] Error: Could not reach api.github.com: {ex.Message}");
                ctx.ExitCode = 1;
                return;
            }

            if (!rateLimitResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[corpus] Error: GitHub API returned {(int)rateLimitResponse.StatusCode}. Check your token.");
                ctx.ExitCode = 1;
                return;
            }

            Console.WriteLine("[corpus] ✓ Connected to api.github.com");
            Console.WriteLine();

            await using var stream = await rateLimitResponse.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("resources", out var resources))
            {
                Console.Error.WriteLine("[corpus] Error: Unexpected rate_limit response shape.");
                ctx.ExitCode = 1;
                return;
            }

            var buckets = new[] { "core", "search", "graphql" };

            const int colBucket    = 10;
            const int colLimit     = 7;
            const int colUsed      = 8;
            const int colRemaining = 11;
            const int colResets    = 11;

            Console.WriteLine($"  {"Bucket",-colBucket}  {"Limit",-colLimit}  {"Used",-colUsed}  {"Remaining",-colRemaining}  {"Resets In",-colResets}");
            Console.WriteLine($"  {"--------",-colBucket}  {"-----",-colLimit}  {"----",-colUsed}  {"---------",-colRemaining}  {"---------",-colResets}");

            foreach (var bucket in buckets)
            {
                if (!resources.TryGetProperty(bucket, out var b))
                    continue;

                var limit     = b.TryGetProperty("limit",     out var lEl) ? lEl.GetInt32() : 0;
                var used      = b.TryGetProperty("used",      out var uEl) ? uEl.GetInt32() : 0;
                var remaining = b.TryGetProperty("remaining", out var rEl) ? rEl.GetInt32() : 0;
                var resetEpoch = b.TryGetProperty("reset",    out var reEl) ? reEl.GetInt64() : 0;

                var resetAt  = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);
                var timeLeft = resetAt - DateTimeOffset.UtcNow;
                var resetsIn = timeLeft.TotalSeconds <= 0
                    ? "0m 0s"
                    : $"{(int)timeLeft.TotalMinutes}m {timeLeft.Seconds:D2}s";

                string remainingDisplay;
                if (bucket == "search")
                {
                    var pct = limit > 0 ? (double)remaining / limit : 1.0;
                    remainingDisplay = pct < 0.2
                        ? $"{remaining} ⚠"
                        : $"{remaining} ✓";
                }
                else
                {
                    remainingDisplay = remaining.ToString();
                }

                Console.WriteLine($"  {bucket,-colBucket}  {limit,-colLimit}  {used,-colUsed}  {remainingDisplay,-colRemaining}  {resetsIn,-colResets}");
            }

            Console.WriteLine();

            if (!File.Exists(dbPath))
            {
                Console.WriteLine("[corpus] (DB not found: skipping recent errors)");
                return;
            }

            Console.WriteLine("[corpus] Recent pipeline errors (last 5):");

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                using var errCmd = db.Connection.CreateCommand();
                errCmd.CommandText = "SELECT recorded_at, step, provider, repo, error_code, message FROM pipeline_errors ORDER BY id DESC LIMIT 5";
                using var reader  = await errCmd.ExecuteReaderAsync(ct);
                bool any = false;
                while (await reader.ReadAsync(ct))
                {
                    any = true;
                    var at   = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var step = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var prov = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var repo = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var code = reader.IsDBNull(4) ? "" : reader.GetInt32(4).ToString();
                    var msg  = reader.IsDBNull(5) ? "" : reader.GetString(5);
                    if (msg.Length > 60) msg = msg[..57] + "...";
                    Console.WriteLine($"  [{at}] {step}/{prov} {repo} ({code}): {msg}");
                }
                if (!any)
                    Console.WriteLine("  (none)");
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
                    Console.WriteLine("[corpus] purge: no filters specified: nothing to do.");
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
                    Console.WriteLine("[corpus] purge: no fixtures matched the filter: corpus is clean.");
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
                            Console.Error.WriteLine($"[corpus] purge: warning: could not delete {path}: {ex.Message}");
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

    // ── gauntletci corpus rejected-repos ──────────────────────────────────────

    private static Command CreateRejectedRepos()
    {
        var dbOpt       = new Option<string>("--db", () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var namesOnlyOpt = new Option<bool>("--names-only", () => false, "Print only owner/repo names, one per line");

        var cmd = new Command("rejected-repos", "List repositories permanently rejected during corpus hydration");
        cmd.AddOption(dbOpt);
        cmd.AddOption(namesOnlyOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var namesOnly = ctx.ParseResult.GetValueForOption(namesOnlyOpt);
            var ct        = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                using var listCmd = db.Connection.CreateCommand();
                listCmd.CommandText = """
                    SELECT repo_owner, repo_name, reason, source, first_rejected_at_utc, last_rejected_at_utc
                    FROM repo_rejections
                    ORDER BY repo_owner, repo_name
                    """;

                using var reader = await listCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var repoSpec = $"{reader.GetString(0)}/{reader.GetString(1)}";
                    if (namesOnly)
                    {
                        Console.WriteLine(repoSpec);
                        continue;
                    }

                    var reason    = reader.GetString(2);
                    var source    = reader.GetString(3);
                    var firstSeen = reader.GetString(4);
                    var lastSeen  = reader.GetString(5);
                    Console.WriteLine($"{repoSpec} | {reason} | source={source} | first={firstSeen} | last={lastSeen}");
                }
            }
        });

        return cmd;
    }

    // ── gauntletci corpus errors ──────────────────────────────────────────────

    private static Command CreateErrors()
    {
        var dbOpt    = new Option<string>("--db",    () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var stepOpt  = new Option<string?>("--step", "Filter by pipeline step (e.g. discover)");
        var limitOpt = new Option<int>("--limit",    () => 50, "Max rows to show");

        var cmd = new Command("errors", "Show recent pipeline errors logged to the corpus database");
        cmd.AddOption(dbOpt);
        cmd.AddOption(stepOpt);
        cmd.AddOption(limitOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var step   = ctx.ParseResult.GetValueForOption(stepOpt);
            var limit  = ctx.ParseResult.GetValueForOption(limitOpt);
            var ct     = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                using var cmd2 = db.Connection.CreateCommand();
                cmd2.CommandText = step is not null
                    ? "SELECT recorded_at, step, provider, repo, error_code, message FROM pipeline_errors WHERE step = $step ORDER BY id DESC LIMIT $limit"
                    : "SELECT recorded_at, step, provider, repo, error_code, message FROM pipeline_errors ORDER BY id DESC LIMIT $limit";
                cmd2.Parameters.AddWithValue("$limit", limit);
                if (step is not null) cmd2.Parameters.AddWithValue("$step", step);

                using var reader = await cmd2.ExecuteReaderAsync(ct);
                bool any = false;
                Console.WriteLine($"{"Time",-22} {"Step",-10} {"Provider",-12} {"Repo",-30} {"Code",-6} Message");
                Console.WriteLine(new string('-', 100));
                while (await reader.ReadAsync(ct))
                {
                    any = true;
                    var recordedAt = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var stepVal    = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var prov       = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var repo       = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var code       = reader.IsDBNull(4) ? "" : reader.GetInt32(4).ToString();
                    var msgVal     = reader.IsDBNull(5) ? "" : reader.GetString(5);
                    if (msgVal.Length > 80) msgVal = msgVal[..77] + "...";
                    Console.WriteLine($"{recordedAt,-22} {stepVal,-10} {prov,-12} {repo,-30} {code,-6} {msgVal}");
                }
                if (!any)
                    Console.WriteLine("  (no errors recorded)");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus sonarcloud enrich ──────────────────────────────────

    private static Command CreateSonarCloudEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");

        var cmd = new Command("enrich", "Cross-reference corpus fixtures against SonarCloud open issues");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                // Load all fixtures for the requested tier from the DB
                using var selectCmd = db.Connection.CreateCommand();
                selectCmd.CommandText = """
                    SELECT fixture_id, repo, tier
                    FROM fixtures
                    WHERE LOWER(tier) = LOWER($tier)
                    ORDER BY repo, fixture_id
                    """;
                selectCmd.Parameters.AddWithValue("$tier", tier);

                var fixtures = new List<FixtureMetadata>();
                using var reader = await selectCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var tierParsed = Enum.TryParse<FixtureTier>(reader.GetString(2), ignoreCase: true, out var t)
                        ? t : FixtureTier.Silver;
                    fixtures.Add(new FixtureMetadata
                    {
                        FixtureId = reader.GetString(0),
                        Repo      = reader.GetString(1),
                        Tier      = tierParsed,
                    });
                }

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures via SonarCloud...");
                Console.WriteLine();

                using var enricher = new SonarCloudEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures,
                    fixturesPath,
                    db,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                Console.WriteLine();
                Console.WriteLine("-- SonarCloud Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed   : {result.FixturesProcessed}");
                Console.WriteLine($"  Fixtures with matches: {result.FixturesWithMatches}");
                Console.WriteLine($"  Total sonar_matches  : {result.TotalMatches}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus codescanning enrich ─────────────────────────────────

    private static Command CreateCodeScanningEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");

        var cmd = new Command("enrich", "Cross-reference corpus fixtures against GitHub CodeQL open alerts (requires GITHUB_TOKEN)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                using var selectCmd = db.Connection.CreateCommand();
                selectCmd.CommandText = """
                    SELECT fixture_id, repo, tier
                    FROM fixtures
                    WHERE LOWER(tier) = LOWER($tier)
                    ORDER BY repo, fixture_id
                    """;
                selectCmd.Parameters.AddWithValue("$tier", tier);

                var fixtures = new List<FixtureMetadata>();
                using var reader = await selectCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var tierParsed = Enum.TryParse<FixtureTier>(reader.GetString(2), ignoreCase: true, out var t)
                        ? t : FixtureTier.Silver;
                    fixtures.Add(new FixtureMetadata
                    {
                        FixtureId = reader.GetString(0),
                        Repo      = reader.GetString(1),
                        Tier      = tierParsed,
                    });
                }

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures via GitHub Code Scanning (CodeQL)...");
                Console.WriteLine();

                using var enricher = new CodeScanningEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures,
                    fixturesPath,
                    db,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing)
                {
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("-- Code Scanning Enrichment Summary --");
                Console.WriteLine($"  Repos with scanning    : {result.ReposWithScanning}");
                Console.WriteLine($"  Repos without scanning : {result.ReposWithoutScanning}");
                Console.WriteLine($"  Fixtures processed     : {result.FixturesProcessed}");
                Console.WriteLine($"  Fixtures with matches  : {result.FixturesWithMatches}");
                Console.WriteLine($"  Total matches written  : {result.TotalMatches}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus fetch-diffs ────────────────────────────────────────

    private static Command CreateFetchDiffs()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to process (Silver|discovery|gold)");
        var limitOpt    = new Option<int?>  ("--limit",                                          "Maximum number of diffs to fetch (default: all)");
        var delayMsOpt  = new Option<int>   ("--delay-ms", () => 1000,                           "Delay in ms between GitHub API requests");
        var dryRunOpt   = new Option<bool>  ("--dry-run",  () => false,                          "List missing diffs without fetching");

        var cmd = new Command("fetch-diffs", "Fetch missing diff.patch files for existing fixtures from GitHub");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayMsOpt);
        cmd.AddOption(dryRunOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs      = ctx.ParseResult.GetValueForOption(delayMsOpt);
            var dryRun       = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var ct           = ctx.GetCancellationToken();

            var token = GitHubTokenResolver.Resolve();
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[fetch-diffs] No GitHub token found. Set GITHUB_TOKEN or run 'gh auth login'.");
                ctx.ExitCode = 1;
                return;
            }

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                using var selectCmd = db.Connection.CreateCommand();
                selectCmd.CommandText = """
                    SELECT fixture_id, repo, pr_number
                    FROM fixtures
                    WHERE LOWER(tier) = LOWER($tier)
                    ORDER BY repo, fixture_id
                    """;
                selectCmd.Parameters.AddWithValue("$tier", tier);

                var allFixtures = new List<(string FixtureId, string Repo, long PrNumber)>();
                using (var reader = await selectCmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                        allFixtures.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
                }

                var missing = allFixtures
                    .Where(f => !File.Exists(Path.Combine(fixturesPath, tier.ToLowerInvariant(), f.FixtureId, "diff.patch")))
                    .ToList();

                if (limit.HasValue)
                    missing = missing.Take(limit.Value).ToList();

                Console.WriteLine($"[fetch-diffs] {missing.Count} of {allFixtures.Count} fixtures missing diff.patch (tier: {tier})");

                if (dryRun)
                {
                    foreach (var f in missing)
                        Console.WriteLine($"  [dry-run] {f.FixtureId}");
                    return;
                }

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
                http.DefaultRequestHeaders.Add("Authorization", $"token {token}");

                int success = 0, failed = 0, idx = 0;
                foreach (var (fixtureId, repo, prNumber) in missing)
                {
                    ct.ThrowIfCancellationRequested();
                    idx++;

                    var parts = repo.Split('/');
                    if (parts.Length != 2)
                    {
                        Console.Error.WriteLine($"[fetch-diffs] [{idx}/{missing.Count}] Bad repo format '{repo}' for {fixtureId}");
                        failed++;
                        continue;
                    }

                    var apiUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/pulls/{prNumber}";
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                        req.Headers.Accept.Clear();
                        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));

                        using var resp = await http.SendAsync(req, ct);
                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.Error.WriteLine($"[fetch-diffs] [{idx}/{missing.Count}] {fixtureId}: HTTP {(int)resp.StatusCode}");
                            failed++;
                            await Task.Delay(delayMs, ct);
                            continue;
                        }

                        var diff = await resp.Content.ReadAsStringAsync(ct);
                        if (string.IsNullOrWhiteSpace(diff))
                        {
                            Console.Error.WriteLine($"[fetch-diffs] [{idx}/{missing.Count}] {fixtureId}: empty diff");
                            failed++;
                            await Task.Delay(delayMs, ct);
                            continue;
                        }

                        var dir = Path.Combine(fixturesPath, tier.ToLowerInvariant(), fixtureId);
                        Directory.CreateDirectory(dir);
                        await File.WriteAllTextAsync(Path.Combine(dir, "diff.patch"), diff, ct);

                        Console.WriteLine($"[fetch-diffs] [{idx}/{missing.Count}] OK  {fixtureId} ({diff.Length:N0} bytes)");
                        success++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[fetch-diffs] [{idx}/{missing.Count}] ERROR {fixtureId}: {ex.Message}");
                        failed++;
                    }

                    await Task.Delay(delayMs, ct);
                }

                Console.WriteLine($"\n[fetch-diffs] Done: {success} fetched, {failed} failed");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus codescanning check-repos ───────────────────────────

    private static Command CreateCodeScanningCheckRepos()
    {
        var dbOpt            = new Option<string>  ("--db",              () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt      = new Option<string>  ("--fixtures",        () => "./data/fixtures",             "Path to fixtures root directory");
        var tierOpt          = new Option<string>  ("--tier",            () => "Silver",                      "Fixture tier used to determine repo list (Silver|discovery|gold)");
        var reposOpt         = new Option<string[]>("--repos",                                                "Additional repos to check (owner/repo), comma-separated") { AllowMultipleArgumentsPerToken = true };
        var seedCandidatesOpt = new Option<int?>   ("--seed-candidates",                                     "Discover and add this many candidate PRs per CodeQL-enabled repo");

        var cmd = new Command("check-repos", "Check which corpus repos have GitHub Code Scanning (CodeQL) enabled");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(reposOpt);
        cmd.AddOption(seedCandidatesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath         = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath   = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier           = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var extraRepos     = ctx.ParseResult.GetValueForOption(reposOpt) ?? [];
            var seedPerRepo    = ctx.ParseResult.GetValueForOption(seedCandidatesOpt);
            var ct             = ctx.GetCancellationToken();

            var token = GitHubTokenResolver.Resolve();
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("[check-repos] No GitHub token found. Set GITHUB_TOKEN or run 'gh auth login'.");
                ctx.ExitCode = 1;
                return;
            }

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                // Collect repos from DB + extras
                using var selectCmd = db.Connection.CreateCommand();
                selectCmd.CommandText = "SELECT DISTINCT repo FROM fixtures WHERE LOWER(tier) = LOWER($tier) ORDER BY repo";
                selectCmd.Parameters.AddWithValue("$tier", tier);

                var repos = new List<string>();
                using (var reader = await selectCmd.ExecuteReaderAsync(ct))
                    while (await reader.ReadAsync(ct))
                        repos.Add(reader.GetString(0));

                foreach (var r in extraRepos
                    .SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Where(r => r.Contains('/')))
                    if (!repos.Contains(r, StringComparer.OrdinalIgnoreCase))
                        repos.Add(r);

                Console.WriteLine($"[check-repos] Checking {repos.Count} repos for CodeQL via GitHub Actions workflows API...");
                Console.WriteLine();

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
                http.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                var enabled  = new List<string>();
                var disabled = new List<string>();

                foreach (var repo in repos)
                {
                    ct.ThrowIfCancellationRequested();
                    var url = $"https://api.github.com/repos/{repo}/actions/workflows?per_page=100";
                    try
                    {
                        using var resp = await http.GetAsync(url, ct);
                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound ||
                            resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            disabled.Add(repo);
                        }
                        else if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadAsStringAsync(ct);
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            var workflows = doc.RootElement.GetProperty("workflows");
                            bool hasCodeQl = false;
                            foreach (var wf in workflows.EnumerateArray())
                            {
                                var path = wf.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                                var name = wf.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                                if (path.Contains("codeql", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("codeql", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasCodeQl = true;
                                    break;
                                }
                            }
                            if (hasCodeQl)
                            {
                                enabled.Add(repo);
                                Console.WriteLine($"  [YES] {repo}");
                            }
                            else
                            {
                                disabled.Add(repo);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  [???] {repo,-50} HTTP {(int)resp.StatusCode}");
                            disabled.Add(repo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [ERR] {repo,-50} {ex.Message}");
                        disabled.Add(repo);
                    }
                    await Task.Delay(200, ct);
                }

                Console.WriteLine();
                Console.WriteLine($"[check-repos] Summary: {enabled.Count} with CodeQL, {disabled.Count} without");
                if (enabled.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("CodeQL-enabled repos:");
                    foreach (var r in enabled) Console.WriteLine($"  {r}");
                }

                if (enabled.Count > 0 && seedPerRepo.HasValue)
                {
                    Console.WriteLine($"\n[check-repos] Seeding candidates for {enabled.Count} CodeQL repos ({seedPerRepo.Value} PRs each)...");
                    int total = 0;
                    foreach (var repo in enabled)
                    {
                        ct.ThrowIfCancellationRequested();
                        var parts = repo.Split('/');
                        int added = await SeedCodeScanningCandidatesAsync(
                            http, db.Connection, parts[0], parts[1], seedPerRepo.Value, ct);
                        Console.WriteLine($"  {repo}: {added} candidates added");
                        total += added;
                        await Task.Delay(500, ct);
                    }
                    Console.WriteLine($"[check-repos] Seeded {total} total candidates. Run 'corpus batch-hydrate --tier Silver --limit N' to hydrate.");
                }
            }
        });

        return cmd;
    }

    private static async Task<int> SeedCodeScanningCandidatesAsync(
        HttpClient http, Microsoft.Data.Sqlite.SqliteConnection conn,
        string owner, string repo, int limit, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddYears(-1).ToString("O");
        var url   = $"https://api.github.com/repos/{owner}/{repo}/pulls?state=closed&sort=updated&direction=desc&per_page={Math.Min(limit * 2, 100)}";

        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return 0;

        var json = await resp.Content.ReadAsStringAsync(ct);
        System.Text.Json.JsonElement[] prs;
        try { prs = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json) ?? []; }
        catch { return 0; }

        int added = 0;
        foreach (var pr in prs.Take(limit))
        {
            if (!pr.TryGetProperty("number", out var numEl)) continue;
            var prNumber = numEl.GetInt32();
            var prUrl    = $"https://github.com/{owner}/{repo}/pull/{prNumber}";
            var id       = $"{owner}/{repo}#{prNumber}";

            using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT OR IGNORE INTO candidates
                    (id, source, repo_owner, repo_name, pr_number, url, candidate_reason)
                VALUES ($id, 'codescanning-seed', $owner, $repo, $pr, $url, 'repo has active CodeQL alerts')
                """;
            insert.Parameters.AddWithValue("$id",    id);
            insert.Parameters.AddWithValue("$owner", owner);
            insert.Parameters.AddWithValue("$repo",  repo);
            insert.Parameters.AddWithValue("$pr",    prNumber);
            insert.Parameters.AddWithValue("$url",   prUrl);
            if (await insert.ExecuteNonQueryAsync(ct) > 0) added++;
        }

        return added;
    }

    // ── gauntletci corpus dependabot enrich ───────────────────────────────────

    private static Command CreateDependabotEnrich()
    {
        var dbOpt      = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var tierOpt    = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt   = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt   = new Option<int>   ("--delay-ms", () => 200,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Check whether each fixture PR was authored by Dependabot (Tier 1 oracle)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier    = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit   = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct      = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesWithPrAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Checking {fixtures.Count} {tier} fixtures for Dependabot authorship...");
                Console.WriteLine();

                using var enricher = new DependabotEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Dependabot Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed  : {result.FixturesProcessed}");
                Console.WriteLine($"  Dependabot fixtures : {result.DependabotFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus dependabot discover ─────────────────────────────────

    private static Command CreateDependabotDiscover()
    {
        var dbOpt        = new Option<string>("--db",         () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var startDateOpt = new Option<string>("--start-date", () => DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd"), "Start date for GH Archive scan (yyyy-MM-dd)");
        var endDateOpt   = new Option<string>("--end-date",   () => DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), "End date for GH Archive scan (yyyy-MM-dd)");
        var limitOpt     = new Option<int>   ("--limit",      () => 200,                           "Max Dependabot PR candidates to seed");

        var cmd = new Command("discover", "Scan GH Archive to find Dependabot PRs in C# repos and seed them as candidates");
        cmd.AddOption(dbOpt);
        cmd.AddOption(startDateOpt);
        cmd.AddOption(endDateOpt);
        cmd.AddOption(limitOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var startStr  = ctx.ParseResult.GetValueForOption(startDateOpt)!;
            var endStr    = ctx.ParseResult.GetValueForOption(endDateOpt)!;
            var limit     = ctx.ParseResult.GetValueForOption(limitOpt);
            var ct        = ctx.GetCancellationToken();

            if (!DateTime.TryParse(startStr, out var startDate) ||
                !DateTime.TryParse(endStr,   out var endDate))
            {
                Console.Error.WriteLine("Invalid date format. Use yyyy-MM-dd.");
                ctx.ExitCode = 1;
                return;
            }

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                Console.WriteLine($"Scanning GH Archive {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} for Dependabot C# PRs...");

                int seeded = 0;
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Corpus/1.0");

                for (var d = startDate.Date; d <= endDate.Date && seeded < limit; d = d.AddDays(1))
                {
                    for (int h = 0; h < 24 && seeded < limit; h++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var url = $"https://data.gharchive.org/{d:yyyy-MM-dd}-{h}.json.gz";

                        byte[]? compressed;
                        try { compressed = await http.GetByteArrayAsync(url, ct); }
                        catch { continue; }

                        using var mem    = new System.IO.MemoryStream(compressed);
                        using var gz     = new System.IO.Compression.GZipStream(mem, System.IO.Compression.CompressionMode.Decompress);
                        using var reader = new System.IO.StreamReader(gz);

                        string? line;
                        while ((line = await reader.ReadLineAsync(ct)) is not null && seeded < limit)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(line);
                                var root = doc.RootElement;

                                if (!root.TryGetProperty("type", out var typeEl) ||
                                    typeEl.GetString() != "PullRequestEvent") continue;

                                if (!root.TryGetProperty("payload", out var payload)) continue;
                                if (!payload.TryGetProperty("action", out var actionEl) ||
                                    actionEl.GetString() != "closed") continue;
                                if (!payload.TryGetProperty("pull_request", out var pr)) continue;
                                if (!pr.TryGetProperty("merged", out var mergedEl) || !mergedEl.GetBoolean()) continue;

                                // Only Dependabot PRs
                                if (!pr.TryGetProperty("user", out var user)) continue;
                                if (!user.TryGetProperty("login", out var loginEl)) continue;
                                var login = loginEl.GetString() ?? "";
                                if (!login.StartsWith("dependabot", StringComparison.OrdinalIgnoreCase)) continue;

                                // Only C# repos
                                var language = "";
                                if (pr.TryGetProperty("base", out var baseEl) &&
                                    baseEl.TryGetProperty("repo", out var baseRepo) &&
                                    baseRepo.TryGetProperty("language", out var langEl) &&
                                    langEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                                    language = langEl.GetString() ?? "";

                                if (!string.Equals(language, "C#", StringComparison.OrdinalIgnoreCase)) continue;

                                if (!root.TryGetProperty("repo", out var repoEl)) continue;
                                if (!repoEl.TryGetProperty("name", out var nameEl)) continue;
                                var repoFull = nameEl.GetString() ?? "";
                                var parts = repoFull.Split('/', 2);
                                if (parts.Length < 2) continue;

                                var prNumber = pr.TryGetProperty("number", out var numEl) ? numEl.GetInt32() : 0;
                                if (prNumber == 0) continue;

                                var id     = $"{parts[0]}/{parts[1]}#{prNumber}";
                                var prUrl  = $"https://github.com/{parts[0]}/{parts[1]}/pull/{prNumber}";

                                using var insert = db.Connection.CreateCommand();
                                insert.CommandText = """
                                    INSERT OR IGNORE INTO candidates
                                        (id, source, repo_owner, repo_name, pr_number, url, language, candidate_reason)
                                    VALUES
                                        ($id, 'dependabot-gharchive', $owner, $repo, $pr, $url, $lang, 'Dependabot PR from GH Archive')
                                    """;
                                insert.Parameters.AddWithValue("$id",    id);
                                insert.Parameters.AddWithValue("$owner", parts[0]);
                                insert.Parameters.AddWithValue("$repo",  parts[1]);
                                insert.Parameters.AddWithValue("$pr",    prNumber);
                                insert.Parameters.AddWithValue("$url",   prUrl);
                                insert.Parameters.AddWithValue("$lang",  language);
                                if (await insert.ExecuteNonQueryAsync(ct) > 0)
                                {
                                    seeded++;
                                    Console.WriteLine($"[dependabot-discover] Seeded: {id}");
                                }
                            }
                            catch (System.Text.Json.JsonException) { }
                        }

                        Console.Write($"\r  {d:yyyy-MM-dd}-{h:D2}: {seeded}/{limit} seeded   ");
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"-- Dependabot Discover Summary --");
                Console.WriteLine($"  Candidates seeded: {seeded}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus social-signal enrich ────────────────────────────────

    private static Command CreateSocialSignalEnrich()
    {
        var dbOpt    = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var tierOpt  = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt = new Option<int>   ("--delay-ms", () => 300,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Fetch PR review metadata (time, reviewers, comments) and compute social-signal score (Tier 2 oracle)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier    = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit   = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct      = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesWithPrAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with PR social-signal data...");
                Console.WriteLine();

                using var enricher = new SocialSignalEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Social Signal Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed    : {result.FixturesProcessed}");
                Console.WriteLine($"  Low-validation (<0.3) : {result.LowValidationFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus composite-label apply ───────────────────────────────

    private static Command CreateCompositeLabelApply()
    {
        var dbOpt                 = new Option<string>("--db",                      () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var tierOpt               = new Option<string>("--tier",                    () => "Silver",                      "Fixture tier to label (Silver|discovery|gold)");
        var limitOpt              = new Option<int>   ("--limit",                   () => 0,                             "Max fixtures to process (0 = all)");
        var updateExpectedOpt     = new Option<bool>  ("--update-expected-findings", () => false,                        "Seed expected_findings rows from composite labels (INSERT OR IGNORE - never overwrites gold labels)");

        var cmd = new Command("apply", "Combine all enricher signals into composite ground-truth labels (HIGH_RISK_GHOST, DEPENDABOT_FIX, SILENT_LOGIC_CHANGE, etc.)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(updateExpectedOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath         = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier           = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit          = ctx.ParseResult.GetValueForOption(limitOpt);
            var updateExpected = ctx.ParseResult.GetValueForOption(updateExpectedOpt);
            var ct             = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Applying composite labels to {fixtures.Count} {tier} fixtures...");
                if (updateExpected)
                    Console.WriteLine("  (--update-expected-findings enabled: will seed expected_findings rows)");
                Console.WriteLine();

                var labeler = new CompositeLabeler();
                var result  = await labeler.ApplyAsync(
                    fixtures, db, updateExpected,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                Console.WriteLine();
                Console.WriteLine("-- Composite Label Summary --");
                Console.WriteLine($"  Fixtures labeled             : {result.FixturesLabeled}");
                Console.WriteLine($"  DEPENDABOT_FIX               : {result.DependabotFix}");
                Console.WriteLine($"  HIGH_RISK_GHOST              : {result.HighRiskGhost}");
                Console.WriteLine($"  SILENT_LOGIC_CHANGE          : {result.SilentLogicChange}");
                Console.WriteLine($"  UNVALIDATED_BEHAVIORAL_RISK  : {result.UnvalidatedBehavioralRisk}");
                Console.WriteLine($"  HOT_PATH_UNREVIEWED          : {result.HotPathUnreviewed}");
                Console.WriteLine($"  STANDARD_CHANGE              : {result.StandardChange}");
                Console.WriteLine($"  INSUFFICIENT_DATA            : {result.InsufficientData}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus semgrep enrich ─────────────────────────────────────

    private static Command CreateSemgrepEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var configOpt   = new Option<string>("--config",   () => "auto",                        "Semgrep config (e.g. auto, p/default, p/owasp-top-ten)");

        var cmd = new Command("enrich", "Run Semgrep C# ruleset against each fixture's added lines (Tier 1 scanner oracle)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(configOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var config       = ctx.ParseResult.GetValueForOption(configOpt)!;
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Running Semgrep on {fixtures.Count} {tier} fixtures (config={config})...");
                Console.WriteLine();

                var enricher = new SemgrepEnricher(config);
                var result   = await enricher.EnrichAsync(
                    fixtures, db, fixturesPath,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.SemgrepMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Semgrep Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed    : {result.FixturesProcessed}");
                Console.WriteLine($"  Fixtures with findings: {result.FixturesWithFindings}");
                Console.WriteLine($"  Total findings        : {result.TotalFindings}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus structural enrich ───────────────────────────────────

    private static Command CreateStructuralEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 200,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Detect sensitive file paths and fetch file-level commit churn from GitHub (Tier 3 structural enricher)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs      = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with structural data...");
                Console.WriteLine();

                using var enricher = new StructuralEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, fixturesPath, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Structural Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed      : {result.FixturesProcessed}");
                Console.WriteLine($"  Sensitive-path fixtures : {result.SensitivePathFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus review-nlp enrich ───────────────────────────────────

    private static Command CreateReviewNlpEnrich()
    {
        var dbOpt    = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var tierOpt  = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt = new Option<int>   ("--delay-ms", () => 200,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Fetch PR review comments and apply keyword taxonomy to extract rule intent signals");
        cmd.AddOption(dbOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier    = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit   = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct      = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesWithPrAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with review NLP data...");
                Console.WriteLine();

                using var enricher = new ReviewCommentNlpEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Review NLP Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed      : {result.FixturesProcessed}");
                Console.WriteLine($"  Fixtures with matches   : {result.FixturesWithMatches}");
                Console.WriteLine($"  Total taxonomy matches  : {result.TotalMatches}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus file-churn enrich ───────────────────────────────────

    private static Command CreateFileChurnEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 200,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Fetch 90-day per-file commit churn from GitHub and compute hotspot scores");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs      = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with file churn data...");
                Console.WriteLine();

                using var enricher = new FileChurnEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, fixturesPath, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- File Churn Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed   : {result.FixturesProcessed}");
                Console.WriteLine($"  Hotspot fixtures     : {result.HotspotFixtures}");
                Console.WriteLine($"  Total files analyzed : {result.TotalFilesAnalyzed}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus nuget-advisory enrich ────────────────────────────────

    private static Command CreateTestCoverageEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");

        var cmd = new Command("enrich", "Classify changed .cs files as production vs test and detect test coverage gaps");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with test coverage data...");
                Console.WriteLine();

                var result = await TestCoverageEnricher.EnrichAsync(
                    fixtures, db, fixturesPath,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                Console.WriteLine();
                Console.WriteLine("-- Test Coverage Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed  : {result.FixturesProcessed}");
                Console.WriteLine($"  Gap fixtures        : {result.GapFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus diff-entropy enrich ──────────────────────────────────

    private static Command CreateDiffEntropyEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");

        var cmd = new Command("enrich", "Compute Kamei et al. JIT defect prediction entropy features from diffs");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with diff entropy data...");
                Console.WriteLine();

                var result = await DiffEntropyEnricher.EnrichAsync(
                    fixtures, db, fixturesPath,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                Console.WriteLine();
                Console.WriteLine("-- Diff Entropy Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed       : {result.FixturesProcessed}");
                Console.WriteLine($"  High-entropy fixtures    : {result.HighEntropyFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus ef-migration enrich ──────────────────────────────────

    private static Command CreateEfMigrationEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");

        var cmd = new Command("enrich", "Detect EF Core migration files and SQL DDL changes in diffs");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with EF migration data...");
                Console.WriteLine();

                var result = await EFMigrationEnricher.EnrichAsync(
                    fixtures, db, fixturesPath,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                Console.WriteLine();
                Console.WriteLine("-- EF Migration Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed   : {result.FixturesProcessed}");
                Console.WriteLine($"  Migration fixtures   : {result.MigrationFixtures}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus nuget-advisory enrich ────────────────────────────────

    private static Command CreateNuGetAdvisoryEnrich(){
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 200,                           "Delay between GitHub GraphQL API calls (ms)");

        var cmd = new Command("enrich", "Query GHSA for NuGet vulnerabilities in changed packages (GraphQL advisory enricher)");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixturesPath = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var tier         = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit        = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs      = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct           = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with NuGet advisory data...");
                Console.WriteLine();

                using var enricher = new NuGetAdvisoryEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, fixturesPath, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- NuGet Advisory Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed       : {result.FixturesProcessed}");
                Console.WriteLine($"  Fixtures with advisories : {result.FixturesWithAdvisories}");
                Console.WriteLine($"  Total advisories found   : {result.TotalAdvisories}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus pr-description enrich ───────────────────────────────

    private static Command CreatePrDescriptionEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 250,                           "Delay between GitHub API calls (ms)");

        var cmd = new Command("enrich", "Fetch PR title/body quality signals (linked issue, WIP keywords, empty body) from GitHub API");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier    = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit   = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct      = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesWithPrAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with PR description data...");
                Console.WriteLine();

                using var enricher = new PRDescriptionEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- PR Description Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed : {result.FixturesProcessed}");
                Console.WriteLine($"  Empty body count   : {result.EmptyBodyCount}");
                Console.WriteLine($"  Linked issue count : {result.LinkedIssueCount}");
            }
        });

        return cmd;
    }

    // ── gauntletci corpus author-experience enrich ────────────────────────────

    private static Command CreateAuthorExperienceEnrich()
    {
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Base path to fixtures folder");
        var tierOpt     = new Option<string>("--tier",     () => "Silver",                      "Fixture tier to enrich (Silver|discovery|gold)");
        var limitOpt    = new Option<int>   ("--limit",    () => 0,                             "Max fixtures to process (0 = all)");
        var delayOpt    = new Option<int>   ("--delay-ms", () => 400,                           "Delay between fixture GitHub API calls (ms)");

        var cmd = new Command("enrich", "Fetch PR author commit history and first-contributor status from GitHub API");
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(delayOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var dbPath  = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var tier    = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var limit   = ctx.ParseResult.GetValueForOption(limitOpt);
            var delayMs = ctx.ParseResult.GetValueForOption(delayOpt);
            var ct      = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);
            using (db)
            {
                var fixtures = await LoadFixturesWithPrAsync(db, tier, ct);
                if (limit > 0) fixtures = fixtures.Take(limit).ToList();

                Console.WriteLine($"Enriching {fixtures.Count} {tier} fixtures with author experience data...");
                Console.WriteLine();

                using var enricher = new AuthorExperienceEnricher();
                var result = await enricher.EnrichAsync(
                    fixtures, db, delayMs,
                    progress: msg => Console.WriteLine(msg),
                    ct: ct);

                if (result.AuthMissing) { ctx.ExitCode = 1; return; }

                Console.WriteLine();
                Console.WriteLine("-- Author Experience Enrichment Summary --");
                Console.WriteLine($"  Fixtures processed        : {result.FixturesProcessed}");
                Console.WriteLine($"  First contributors        : {result.FirstContributors}");
                Console.WriteLine($"  Low-experience (none/low) : {result.LowExperienceCount}");
            }
        });

        return cmd;
    }

    // ── shared fixture loading helpers ────────────────────────────────────────

    private static async Task<List<FixtureMetadata>> LoadFixturesAsync(
        CorpusDb db, string tier, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT fixture_id, repo, tier
            FROM fixtures
            WHERE LOWER(tier) = LOWER($tier)
            ORDER BY repo, fixture_id
            """;
        cmd.Parameters.AddWithValue("$tier", tier);

        var list = new List<FixtureMetadata>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var tierParsed = Enum.TryParse<FixtureTier>(reader.GetString(2), ignoreCase: true, out var t)
                ? t : FixtureTier.Silver;
            list.Add(new FixtureMetadata
            {
                FixtureId = reader.GetString(0),
                Repo      = reader.GetString(1),
                Tier      = tierParsed,
            });
        }
        return list;
    }

    private static async Task<List<FixtureMetadata>> LoadFixturesWithPrAsync(
        CorpusDb db, string tier, CancellationToken ct)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT fixture_id, repo, tier, pr_number
            FROM fixtures
            WHERE LOWER(tier) = LOWER($tier)
            ORDER BY repo, fixture_id
            """;
        cmd.Parameters.AddWithValue("$tier", tier);

        var list = new List<FixtureMetadata>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var tierParsed = Enum.TryParse<FixtureTier>(reader.GetString(2), ignoreCase: true, out var t)
                ? t : FixtureTier.Silver;
            list.Add(new FixtureMetadata
            {
                FixtureId         = reader.GetString(0),
                Repo              = reader.GetString(1),
                Tier              = tierParsed,
                PullRequestNumber = reader.GetInt32(3),
            });
        }
        return list;
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

    /// <summary>
    /// Detects hardware, selects an appropriate model, ensures Ollama is installed and running,
    /// pulls the model if needed, and returns a ready <see cref="ILlmLabeler"/>.
    /// Falls back to <see cref="NullLlmLabeler"/> with a console message on any failure.
    /// </summary>
    private static async Task<(ILlmLabeler Labeler, int ReadyCount)> SetupOllamaLabelerAsync(
        string? modelOverride, IReadOnlyList<string> baseUrls, CancellationToken ct)
    {
        var normalizedUrls = NormalizeOllamaUrls(baseUrls);

        // 1. Hardware detection: pick best model if user didn't specify one
        var hw = HardwareProfile.Detect();
        var model = !string.IsNullOrWhiteSpace(modelOverride)
            ? modelOverride
            : hw.RecommendedModel;

        Console.WriteLine($"[corpus] Hardware: {hw.ToSummaryString()}");
        Console.WriteLine($"[corpus] Selected model: {model}{(modelOverride != null ? " (user-specified)" : " (auto-selected)")}");

        var hasOllamaCli = OllamaLlmLabeler.IsInstalled();
        var readyEndpoints = new List<LlmEndpoint>();

        foreach (var baseUrl in normalizedUrls)
        {
            var labeler = new OllamaLlmLabeler(model, baseUrl);
            var isLocalEndpoint = IsLocalOllamaUrl(baseUrl);

            if (!await labeler.IsServerRunningAsync(ct))
            {
                if (isLocalEndpoint && hasOllamaCli)
                {
                    Console.WriteLine($"[corpus] Ollama endpoint {baseUrl} not running: attempting to start...");
                    if (!await labeler.TryStartServerAsync(ct: ct))
                    {
                        Console.Error.WriteLine($"[corpus] Could not start Ollama at {baseUrl}. Skipping endpoint.");
                        labeler.Dispose();
                        continue;
                    }

                    Console.WriteLine($"[corpus] Ollama endpoint {baseUrl} started.");
                }
                else
                {
                    Console.Error.WriteLine($"[corpus] Ollama endpoint unavailable: {baseUrl}. Skipping endpoint.");
                    labeler.Dispose();
                    continue;
                }
            }

            if (!await labeler.IsModelAvailableAsync(ct))
            {
                if (isLocalEndpoint && hasOllamaCli)
                {
                    Console.WriteLine($"[corpus] Model '{model}' not found at {baseUrl}: pulling (this may take several minutes)...");
                    var pulled = await labeler.TryPullModelAsync(
                        line => Console.WriteLine($"         {line}"), ct);

                    if (!pulled)
                    {
                        Console.Error.WriteLine($"[corpus] Failed to pull model '{model}' for {baseUrl}. Skipping endpoint.");
                        labeler.Dispose();
                        continue;
                    }

                    Console.WriteLine($"[corpus] Model '{model}' ready at {baseUrl}.");
                }
                else
                {
                    Console.Error.WriteLine($"[corpus] Model '{model}' is unavailable at {baseUrl}. Skipping endpoint.");
                    labeler.Dispose();
                    continue;
                }
            }

            readyEndpoints.Add(new LlmEndpoint(baseUrl, labeler));
        }

        if (readyEndpoints.Count == 0)
        {
            Console.Error.WriteLine("[corpus] No Ollama endpoints are ready. Falling back to NullLlmLabeler.");
            return (new NullLlmLabeler(), 0);
        }

        var enabledUrls = string.Join(", ", readyEndpoints.Select(e => e.Name));
        if (readyEndpoints.Count == 1)
        {
            Console.WriteLine($"[corpus] LLM labeling enabled via Ollama ({enabledUrls}, model: {model})");
            return (readyEndpoints[0].Labeler, 1);
        }

        Console.WriteLine($"[corpus] LLM labeling enabled via Ollama ({readyEndpoints.Count} endpoints, model: {model})");
        Console.WriteLine($"[corpus] Ollama endpoints: {enabledUrls}");
        return (new RoundRobinLlmLabeler(readyEndpoints), readyEndpoints.Count);
    }

    private static IReadOnlyList<string> NormalizeOllamaUrls(IEnumerable<string>? rawUrls)
        => OllamaUrlNormalizer.Normalize(rawUrls);

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

    private static bool IsLocalOllamaUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.IsLoopback
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetLabelAllParallelism(bool llmLabel, string provider, int ollamaUrlCount)
        => llmLabel && provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(1, ollamaUrlCount)
            : 1;

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
