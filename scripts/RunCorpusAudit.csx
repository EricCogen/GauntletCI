#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Data.Sqlite, 7.0.0"
#r "bin/Release/net8.0/GauntletCI.Core.dll"
#r "bin/Release/net8.0/GauntletCI.Corpus.dll"

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using GauntletCI.Corpus.Storage;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Interfaces;

var corpusDb = Environment.GetEnvironmentVariable("CORPUS_DB") ?? $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.gauntletci/corpus.db";
var repoPath = Directory.GetCurrentDirectory();

Console.WriteLine($"📊 GauntletCI Corpus Batch Runner");
Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"Corpus DB: {corpusDb}");
Console.WriteLine($"Repo Path: {repoPath}\n");

// Open database and query fixtures
using var connection = new SqliteConnection($"Data Source={corpusDb}");
await connection.OpenAsync();

using var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT id, fixture_id, repo, pr_number, pr_size_bucket, tier
    FROM fixtures
    ORDER BY created_at_utc DESC
";

var fixtures = new List<(string Id, string FixtureId, string Repo, int PrNumber, string SizeBucket, string Tier)>();
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        fixtures.Add((
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5)
        ));
    }
}

Console.WriteLine($"📦 Found {fixtures.Count} fixtures to audit\n");

// Summary by tier and size
var byTier = fixtures.GroupBy(f => f.Tier);
foreach (var tier in byTier)
{
    Console.WriteLine($"  {tier.Key}: {tier.Count()} fixtures");
    var bySize = tier.GroupBy(f => f.SizeBucket);
    foreach (var size in bySize)
    {
        Console.WriteLine($"    {size.Key}: {size.Count()}");
    }
}

Console.WriteLine($"\n🚀 Ready to re-run audit on all {fixtures.Count} fixtures");
Console.WriteLine($"   Command: dotnet GauntletCI.Corpus run-all --corpus-db {corpusDb}");
Console.WriteLine($"   Tracks: rule_runs, actual_findings in database");
