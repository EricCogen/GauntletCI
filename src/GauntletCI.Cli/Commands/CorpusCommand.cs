// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Cli.Commands;

public static class CorpusCommand
{
    public static Command Create()
    {
        var corpus = new Command("corpus", "Manage the GauntletCI fixture corpus");
        corpus.AddCommand(CreateAddPr());
        corpus.AddCommand(CreateNormalize());
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
