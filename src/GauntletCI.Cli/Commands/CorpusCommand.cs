// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Cli.Commands;

public static class CorpusCommand
{
    public static Command Create()
    {
        var corpus = new Command("corpus", "Manage the GauntletCI fixture corpus");
        corpus.AddCommand(CreateAddPr());
        return corpus;
    }

    // gauntletci corpus add-pr --url <url> [--db <path>] [--fixtures <path>]
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

            var db = new CorpusDb(dbPath);
            await db.InitializeAsync(ct);

            var hydrator   = GitHubRestHydrator.CreateDefault(fixtures);
            var rawStore   = new RawSnapshotStore(fixtures);
            var fixtureStore = new FixtureFolderStore(db, fixtures);

            try
            {
                var hydrated = await hydrator.HydrateFromUrlAsync(url, ct);
                var metadata = FixtureNormalizer.Normalize(hydrated, source: "manual");

                await fixtureStore.SaveMetadataAsync(metadata, ct);

                Console.WriteLine($"[corpus] Saved fixture: {metadata.FixtureId}");
                Console.WriteLine($"[corpus] Tier: {metadata.Tier}  Size: {metadata.PrSizeBucket}  " +
                                  $"Files: {metadata.FilesChanged}  Tags: {string.Join(", ", metadata.Tags)}");
                Console.WriteLine($"[corpus] Run 'gauntletci corpus run --fixture {metadata.FixtureId}' next.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[corpus] Error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }
}
