// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;
using GauntletCI.Core.Configuration;
using GauntletCI.Llm;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Factory for corpus fixture labeling and label validation: label, label-all, reset-stats.
/// Applies silver heuristic labels and optionally LLM-based Tier 3 labels to fixtures.
/// Extracts label source tracking and batch labeling operations.
///
/// Each Create* method builds and returns a System.CommandLine Command with:
/// - Label strategy options (silver heuristics, LLM-based)
/// - Batch operations with progress tracking
/// - Statistics computation and reporting
///
/// Extracted from CorpusCommand to improve maintainability (EI-4, EI-5 compliance).
/// Target: <600 LOC, single responsibility, focused on labeling workflows.
/// </summary>
public static class CorpusLabelingFactory
{
    /// <summary>
    /// Create the 'label' command: Apply silver heuristic labels to a single fixture.
    /// Command: corpus label --fixture &lt;id&gt; [--overwrite] [--db] [--fixtures]
    /// </summary>
    public static Command CreateLabel()
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

                    var diffText = await File.ReadAllTextAsync(diffPath!, ct);
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

    /// <summary>
    /// Create the 'label-all' command: Apply labels to all fixtures in a tier.
    /// Command: corpus label-all [--tier] [--overwrite] [--verbose] [--llm-label] [--llm-provider] [--db] [--fixtures]
    /// </summary>
    public static Command CreateLabelAll()
    {
        var tierOpt         = new Option<string>("--tier",         () => "discovery", "Fixture tier to process (gold|silver|discovery)");
        var overwriteOpt    = new Option<bool>  ("--overwrite",    () => false,       "Overwrite existing HumanReview/Seed labels with heuristic labels");
        var verboseOpt      = new Option<bool>  ("--verbose",      () => false,       "Print per-rule label breakdown for each fixture");
        var llmLabelOpt     = new Option<bool>  ("--llm-label",    () => false,       "Enable LLM-based Tier 3 labeling");
        var llmProviderOpt  = new Option<string>("--llm-provider", () => "ollama",    "LLM provider: ollama | anthropic | github-models | none");
        var llmModelOpt     = new Option<string>("--llm-model",    () => "",          "Model override (provider default used if empty)");
        var llmUrlOpt       = new Option<string[]>("--llm-url",    () => [], "Ollama base URL(s). Repeat the flag or pass a comma-separated list");
        var dbOpt           = new Option<string>("--db",           () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt     = new Option<string>("--fixtures",     () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("label-all", "Apply silver labels to all fixtures in a tier, with optional LLM refinement");
        cmd.AddOption(tierOpt);
        cmd.AddOption(overwriteOpt);
        cmd.AddOption(verboseOpt);
        cmd.AddOption(llmLabelOpt);
        cmd.AddOption(llmProviderOpt);
        cmd.AddOption(llmModelOpt);
        cmd.AddOption(llmUrlOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var tierStr      = ctx.ParseResult.GetValueForOption(tierOpt)!;
            var overwrite    = ctx.ParseResult.GetValueForOption(overwriteOpt);
            var verbose      = ctx.ParseResult.GetValueForOption(verboseOpt);
            var llmLabel     = ctx.ParseResult.GetValueForOption(llmLabelOpt);
            var llmProvider  = ctx.ParseResult.GetValueForOption(llmProviderOpt)!;
            var llmModel     = ctx.ParseResult.GetValueForOption(llmModelOpt)!;
            var llmUrls      = ctx.ParseResult.GetValueForOption(llmUrlOpt) ?? [];
            var dbPath       = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures     = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct           = ctx.GetCancellationToken();

            if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var tier))
            {
                Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                ctx.ExitCode = 1;
                return;
            }

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var all = await store.ListFixturesAsync(tier, ct);
                    if (all.Count == 0)
                    {
                        Console.WriteLine($"[corpus] No fixtures found in tier {tier}");
                        return;
                    }

                    Console.WriteLine($"[corpus] Labeling {all.Count} {tier} fixture(s)...");
                    Console.WriteLine();

                    var engine = new SilverLabelEngine(store);
                    int totalLabeled = 0;
                    int totalLabels = 0;

                    for (int i = 0; i < all.Count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var fixtureId = all[i].FixtureId;

                        string? fixturePath = null;
                        foreach (var t in new[] { FixtureTier.Gold, FixtureTier.Silver, FixtureTier.Discovery })
                        {
                            var candidate = FixtureIdHelper.GetFixturePath(fixtures, t, fixtureId);
                            if (Directory.Exists(candidate)) { fixturePath = candidate; break; }
                        }

                        var diffPath = fixturePath is not null ? Path.Combine(fixturePath, "diff.patch") : null;

                        if (fixturePath is null || !File.Exists(diffPath!))
                        {
                            Console.WriteLine($"  [{i + 1}/{all.Count}] {fixtureId}: SKIP (no diff.patch)");
                            continue;
                        }

                        try
                        {
                            var diffText = await File.ReadAllTextAsync(diffPath, ct);
                            var labelsWritten = await engine.ApplyToFixtureAsync(fixtureId, diffText, overwrite, ct);

                            totalLabeled++;
                            totalLabels += labelsWritten;

                            Console.WriteLine($"  [{i + 1}/{all.Count}] {fixtureId,-40} {labelsWritten,3} label(s)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  [{i + 1}/{all.Count}] {fixtureId,-40} ERR ({ex.Message})");
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine($"[corpus] Label-all complete: {totalLabeled} labeled, {totalLabels} total labels written");

                    if (llmLabel && !string.IsNullOrEmpty(llmProvider) && llmProvider != "none")
                    {
                        Console.WriteLine($"[corpus] LLM-based Tier 3 labeling not yet implemented");
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
    /// Create the 'reset-stats' command: Clear fixture statistics (labels, findings) for recomputation.
    /// Command: corpus reset-stats --fixture &lt;id&gt; | --tier &lt;tier&gt; [--dry-run] [--db] [--fixtures]
    /// </summary>
    public static Command CreateResetStats()
    {
        var fixtureOpt   = new Option<string?>("--fixture",  "Single fixture ID to reset (or use --tier for batch)");
        var tierOpt      = new Option<string?>("--tier",     "Tier to reset (gold|silver|discovery) (or use --fixture for single)");
        var dryRunOpt    = new Option<bool>   ("--dry-run",  () => false, "Print what would be reset without making changes");
        var dbOpt        = new Option<string> ("--db",       () => "./data/gauntletci-corpus.db", "Path to corpus SQLite database");
        var fixturesOpt  = new Option<string> ("--fixtures", () => "./data/fixtures",             "Path to fixtures root directory");

        var cmd = new Command("reset-stats", "Clear fixture statistics for recomputation");
        cmd.AddOption(fixtureOpt);
        cmd.AddOption(tierOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(fixturesOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var fixtureId = ctx.ParseResult.GetValueForOption(fixtureOpt);
            var tierStr   = ctx.ParseResult.GetValueForOption(tierOpt);
            var dryRun    = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var dbPath    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var fixtures  = ctx.ParseResult.GetValueForOption(fixturesOpt)!;
            var ct        = ctx.GetCancellationToken();

            if (string.IsNullOrEmpty(fixtureId) && string.IsNullOrEmpty(tierStr))
            {
                Console.Error.WriteLine("[corpus] reset-stats: Specify either --fixture or --tier");
                ctx.ExitCode = 1;
                return;
            }

            FixtureTier? parsedTier = null;
            if (!string.IsNullOrEmpty(tierStr))
            {
                if (!Enum.TryParse<FixtureTier>(tierStr, ignoreCase: true, out var tier))
                {
                    Console.Error.WriteLine($"[corpus] Unknown tier '{tierStr}'. Use gold, silver, or discovery.");
                    ctx.ExitCode = 1;
                    return;
                }
                parsedTier = tier;
            }

            var (db, store, _) = await CorpusCommandHelpers.BuildPipeline(dbPath, fixtures, ct);
            using (db)
            {
                try
                {
                    var toReset = new List<string>();

                    if (!string.IsNullOrEmpty(fixtureId))
                    {
                        toReset.Add(fixtureId);
                    }
                    else if (parsedTier.HasValue)
                    {
                        var all = await store.ListFixturesAsync(parsedTier.Value, ct);
                        toReset.AddRange(all.Select(m => m.FixtureId));
                    }

                    Console.WriteLine($"[corpus] reset-stats: Clearing stats for {toReset.Count} fixture(s){(dryRun ? " (dry-run)" : "")}");

                    foreach (var fid in toReset)
                    {
                        using var cmd2 = db.Connection.CreateCommand();
                        cmd2.CommandText = """
                            UPDATE fixtures SET
                              label_count = 0,
                              finding_count = 0,
                              last_labeled_at = NULL
                            WHERE fixture_id = $id
                            """;
                        cmd2.Parameters.AddWithValue("$id", fid);

                        if (!dryRun)
                            await cmd2.ExecuteNonQueryAsync(ct);

                        Console.WriteLine($"  {fid}");
                    }

                    if (!dryRun)
                        Console.WriteLine($"[corpus] reset-stats: Stats cleared for {toReset.Count} fixture(s)");
                    else
                        Console.WriteLine($"[corpus] reset-stats: (dry-run) No changes made");
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
