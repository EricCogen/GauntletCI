// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Factory for corpus management utilities: purge, errors, rejected-repos, doctor.
/// Handles cleanup, diagnostics, and health checks of the corpus database and fixtures.
///
/// Each Create* method builds and returns a System.CommandLine Command with:
/// - Fixture quality checks (language, review comments)
/// - Error log retrieval and filtering
/// - Diagnostic operations and health reporting
/// - Dry-run preview and batch operations
///
/// Extracted from CorpusCommand to improve maintainability (EI-4, EI-5 compliance).
/// Target: <600 LOC, single responsibility, focused on corpus maintenance.
/// </summary>
public static class CorpusUtilityFactory
{
    /// <summary>
    /// Create the 'purge' command: Remove low-quality fixtures from the corpus.
    /// Command: corpus purge [--language] [--require-review-comments] [--repo-blocklist] [--dry-run] [--db] [--fixtures]
    /// </summary>
    public static Command CreatePurge()
    {
        var languageOpt              = new Option<string>("--language",              () => "C#",  "Remove fixtures whose inferred language doesn't match this value");
        var requireReviewCommentsOpt = new Option<bool>("--require-review-comments", () => false, "Remove fixtures that have no inline review comments");
        var repoBlocklistOpt         = new Option<string[]>("--repo-blocklist",      "Remove fixtures from these owner/repo names") { AllowMultipleArgumentsPerToken = false, Arity = ArgumentArity.ZeroOrMore };
        var dryRunOpt                = new Option<bool>  ("--dry-run",              () => false, "Print what would be purged without making changes");
        var dbOpt                    = new Option<string>("--db",                   () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt              = new Option<string>("--fixtures",             () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("purge", "Remove low-quality fixtures from the corpus");
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

            var (db, _, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixturesRoot, ct);
            using (db)
            {
                try
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

                    if (!dryRun)
                    {
                        using var deleteCmd = db.Connection.CreateCommand();
                        deleteCmd.CommandText = "DELETE FROM fixtures WHERE fixture_id = $id";

                        foreach (var (fid, path, repo, prn) in toPurge)
                        {
                            deleteCmd.Parameters.Clear();
                            deleteCmd.Parameters.AddWithValue("$id", fid);
                            await deleteCmd.ExecuteNonQueryAsync(ct);

                            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                                Directory.Delete(path, recursive: true);
                        }

                        Console.WriteLine($"[corpus] purge: {toPurge.Count} fixture(s) deleted");
                    }
                    else
                    {
                        Console.WriteLine($"[corpus] purge: (dry-run) {toPurge.Count} fixture(s) would be deleted");
                    }
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
    /// Create the 'errors' command: View pipeline errors logged during discovery/hydration/labeling.
    /// Command: corpus errors [--step] [--repo] [--limit] [--db]
    /// </summary>
    public static Command CreateErrors()
    {
        var stepOpt   = new Option<string?>("--step",  "Filter by pipeline step (discover|hydrate|label|run)");
        var repoOpt   = new Option<string?>("--repo",  "Filter by repo owner/repo");
        var limitOpt  = new Option<int>    ("--limit", () => 50, "Max errors to display");
        var dbOpt     = new Option<string> ("--db",    () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");

        var cmd = new Command("errors", "View pipeline errors logged during corpus operations");
        cmd.AddOption(stepOpt);
        cmd.AddOption(repoOpt);
        cmd.AddOption(limitOpt);
        cmd.AddOption(dbOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var step  = ctx.ParseResult.GetValueForOption(stepOpt);
            var repo  = ctx.ParseResult.GetValueForOption(repoOpt);
            var limit = ctx.ParseResult.GetValueForOption(limitOpt);
            var dbPath = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var ct    = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);

            try
            {
                using var cmd2 = db.Connection.CreateCommand();
                var where = new List<string>();
                if (!string.IsNullOrEmpty(step))
                    where.Add("step = $step");
                if (!string.IsNullOrEmpty(repo))
                    where.Add("repo LIKE $repo");

                var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
                cmd2.CommandText = $"""
                    SELECT logged_at, step, provider, repo, error_code, error_message
                    FROM pipeline_errors
                    {whereClause}
                    ORDER BY logged_at DESC
                    LIMIT $limit
                    """;

                if (!string.IsNullOrEmpty(step))
                    cmd2.Parameters.AddWithValue("$step", step);
                if (!string.IsNullOrEmpty(repo))
                    cmd2.Parameters.AddWithValue("$repo", $"%{repo}%");
                cmd2.Parameters.AddWithValue("$limit", limit);

                Console.WriteLine("[corpus] Pipeline errors:");
                Console.WriteLine();

                bool any = false;
                using (var reader = await cmd2.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        any = true;
                        var at   = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        var step2 = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        var prov = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        var repo2 = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        var code = reader.IsDBNull(4) ? "" : reader.GetInt32(4).ToString();
                        var msg  = reader.IsDBNull(5) ? "" : reader.GetString(5);
                        if (msg.Length > 60) msg = msg[..57] + "...";
                        Console.WriteLine($"  [{at}] {step2}/{prov} {repo2} ({code}): {msg}");
                    }
                }

                if (!any)
                    Console.WriteLine("  (none)");
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
    /// Create the 'rejected-repos' command: List repositories that triggered fixture rejection during hydration.
    /// Command: corpus rejected-repos [--limit] [--db]
    /// </summary>
    public static Command CreateRejectedRepos()
    {
        var limitOpt = new Option<int>   ("--limit", () => 20, "Max repos to display");
        var dbOpt    = new Option<string>("--db",    () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");

        var cmd = new Command("rejected-repos", "List repositories that had fixtures rejected during hydration");
        cmd.AddOption(limitOpt);
        cmd.AddOption(dbOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var limit  = ctx.ParseResult.GetValueForOption(limitOpt);
            var dbPath = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var ct     = ctx.GetCancellationToken();

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);

            try
            {
                using var cmd2 = db.Connection.CreateCommand();
                cmd2.CommandText = """
                    SELECT repo, COUNT(*) as rejection_count
                    FROM pipeline_errors
                    WHERE step = 'hydrate'
                    GROUP BY repo
                    ORDER BY rejection_count DESC
                    LIMIT $limit
                    """;
                cmd2.Parameters.AddWithValue("$limit", limit);

                Console.WriteLine("[corpus] Rejected repositories (by hydration failures):");
                Console.WriteLine();

                const int colRepo = 30;
                const int colCount = 5;
                Console.WriteLine($"{"Repo",-colRepo}  {"Count",colCount}");
                Console.WriteLine(new string('-', colRepo + colCount + 4));

                using (var reader = await cmd2.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var repo = reader.IsDBNull(0) ? "(unknown)" : reader.GetString(0);
                        var count = reader.GetInt32(1);
                        Console.WriteLine($"{repo,-colRepo}  {count,colCount}");
                    }
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
    /// Create the 'doctor' command: Run corpus health checks and diagnostics.
    /// Command: corpus doctor [--verbose] [--db] [--fixtures]
    /// </summary>
    public static Command CreateDoctor()
    {
        var verboseOpt  = new Option<bool>  ("--verbose",  () => false, "Print detailed diagnostic output");
        var dbOpt       = new Option<string>("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt = new Option<string>("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("doctor", "Run corpus health checks and diagnostics");
        cmd.AddOption(verboseOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var verbose  = ctx.ParseResult.GetValueForOption(verboseOpt);
            var dbPath   = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct       = ctx.GetCancellationToken();

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("╔════════════════════════════════════════╗");
                    Console.WriteLine("║         CORPUS HEALTH CHECK            ║");
                    Console.WriteLine("╚════════════════════════════════════════╝");
                    Console.WriteLine();

                    var all = await store.ListFixturesAsync(null, ct);
                    Console.WriteLine($"Total fixtures: {all.Count}");

                    var byTier = all.GroupBy(m => m.Tier).ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var (tierKey, tierList) in byTier.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {tierKey,-10}: {tierList.Count,4}");
                    }

                    // Check for orphaned fixtures (missing diff.patch)
                    int orphaned = 0;
                    foreach (var fixture in all)
                    {
                        string? fixturePath = null;
                        foreach (var t in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
                        {
                            var candidate = FixtureIdHelper.GetFixturePath(fixtures, t, fixture.FixtureId);
                            if (Directory.Exists(candidate)) { fixturePath = candidate; break; }
                        }

                        if (fixturePath is null || !File.Exists(Path.Combine(fixturePath, "diff.patch")))
                            orphaned++;
                    }

                    Console.WriteLine();
                    if (orphaned > 0)
                    {
                        Console.WriteLine($"⚠ Warning: {orphaned} fixture(s) missing diff.patch (orphaned)");
                    }
                    else
                    {
                        Console.WriteLine("✓ All fixtures have diff.patch");
                    }

                    // Check pipeline errors
                    using var errCmd = db.Connection.CreateCommand();
                    errCmd.CommandText = "SELECT COUNT(*) FROM pipeline_errors";
                    var errorCountObj = await errCmd.ExecuteScalarAsync(ct);
                    var errorCount = errorCountObj is long count ? count : 0;
                    Console.WriteLine();
                    if (errorCount > 0)
                    {
                        Console.WriteLine($"⚠ Warning: {errorCount} pipeline error(s) logged (run 'corpus errors' to review)");
                    }
                    else
                    {
                        Console.WriteLine("✓ No pipeline errors");
                    }

                    Console.WriteLine();
                    if (verbose)
                    {
                        Console.WriteLine("Language distribution:");
                        var langCounts = all
                            .GroupBy(m => m.Language)
                            .OrderByDescending(g => g.Count())
                            .Take(10);
                        foreach (var lang in langCounts)
                        {
                            Console.WriteLine($"  {lang.Key,12}: {lang.Count(),4} fixture(s)");
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("Status: ", ConsoleColor.Green);
                    if (orphaned == 0 && errorCount == 0)
                        Console.WriteLine("✓ Corpus is healthy");
                    else
                        Console.WriteLine("⚠ Issues detected - review above warnings");
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
