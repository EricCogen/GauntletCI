// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Shared helper methods used by corpus command factories.
/// Extracted from CorpusCommand to reduce duplication and improve testability.
/// </summary>
public static class CorpusCommandHelpers
{
    /// <summary>
    /// Initialize database, fixture store, and normalization pipeline.
    /// </summary>
    public static async Task<(CorpusDb Db, FixtureFolderStore Store, NormalizationPipeline Pipeline)>
        BuildPipeline(string dbPath, string fixturesPath, CancellationToken ct)
    {
        var db       = new CorpusDb(dbPath);
        await db.InitializeAsync(ct);
        var store    = new FixtureFolderStore(db, fixturesPath);
        var pipeline = new NormalizationPipeline(store);
        return (db, store, pipeline);
    }

    /// <summary>
    /// Print fixture metadata in human-readable format.
    /// </summary>
    public static void PrintMetadata(FixtureMetadata m)
    {
        Console.WriteLine($"  Repo         : {m.Repo}");
        Console.WriteLine($"  PR #         : {m.PullRequestNumber}");
        Console.WriteLine($"  Language     : {m.Language}");
        Console.WriteLine($"  Tier         : {m.Tier}");
        Console.WriteLine($"  Tags         : {string.Join(", ", m.Tags)}");
    }

    /// <summary>
    /// Print fixture list as formatted table.
    /// </summary>
    public static void PrintFixtureTable(List<FixtureMetadata> fixtures)
    {
        if (fixtures.Count == 0)
        {
            Console.WriteLine("No fixtures found.");
            return;
        }

        var maxRepo = Math.Max(20, fixtures.Max(f => f.Repo.Length));
        var maxId = Math.Max(20, fixtures.Max(f => f.FixtureId.Length));

        Console.WriteLine();
        Console.WriteLine($"{"ID".PadRight(maxId)}  {"Repo".PadRight(maxRepo)}  {"PR",6}  {"Lang",5}  {"Tier",10}");
        Console.WriteLine(new string('-', maxId + maxRepo + 40));

        foreach (var f in fixtures.OrderBy(x => x.FixtureId))
        {
            Console.WriteLine($"{f.FixtureId.PadRight(maxId)}  {f.Repo.PadRight(maxRepo)}  {f.PullRequestNumber,6}  {f.Language,5}  {f.Tier,10}");
        }

        Console.WriteLine($"\nTotal: {fixtures.Count} fixture(s)\n");
    }

    /// <summary>
    /// Print fixture list as JSON.
    /// </summary>
    public static void PrintAsJson(List<FixtureMetadata> fixtures)
    {
        var jsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        Console.WriteLine(JsonSerializer.Serialize(fixtures, jsonOpts));
    }

    /// <summary>
    /// Find git repository root by walking up directory tree.
    /// </summary>
    public static string? FindGitRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}
