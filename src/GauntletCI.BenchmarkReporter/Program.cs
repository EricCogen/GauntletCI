// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.BenchmarkReporter;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;

string fixturesArg = "tests/GauntletCI.Benchmarks/Fixtures/curated";
string outputArg = "docs/benchmarks";

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--fixtures") fixturesArg = args[i + 1];
    if (args[i] == "--output") outputArg = args[i + 1];
}

var fixturesRoot = Path.IsPathRooted(fixturesArg)
    ? fixturesArg
    : Path.Combine(Directory.GetCurrentDirectory(), fixturesArg);

var outputDir = Path.IsPathRooted(outputArg)
    ? outputArg
    : Path.Combine(Directory.GetCurrentDirectory(), outputArg);

if (!Directory.Exists(fixturesRoot))
{
    Console.Error.WriteLine($"Fixtures directory not found: {fixturesRoot}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
};

// Collect all fixture entries
var allEntries = new List<(string Dir, FixtureEntry Entry)>();

foreach (var dir in Directory.GetDirectories(fixturesRoot).OrderBy(d => d))
{
    var manifestPath = Path.Combine(dir, "manifest.json");
    if (!File.Exists(manifestPath)) continue;

    FixtureManifest? manifest;
    try
    {
        var json = File.ReadAllText(manifestPath);
        manifest = JsonSerializer.Deserialize<FixtureManifest>(json, jsonOpts);
    }
    catch { continue; }

    if (manifest?.Fixtures is null) continue;

    foreach (var entry in manifest.Fixtures)
    {
        var diffPath = Path.Combine(dir, entry.DiffFile);
        if (File.Exists(diffPath))
            allEntries.Add((dir, entry));
    }
}

Console.WriteLine($"Running {allEntries.Count} fixtures from {fixturesRoot}...");

// Per-rule accumulators
var ruleAccumulators = new Dictionary<string, (int Tp, int Fp, int Fn, int Tn, string Desc)>();
int aggTp = 0, aggFp = 0, aggFn = 0, aggTn = 0;

var orchestrator = RuleOrchestrator.CreateDefault();

foreach (var (dir, entry) in allEntries)
{
    var diffPath = Path.Combine(dir, entry.DiffFile);
    var rawDiff = await File.ReadAllTextAsync(diffPath);
    var diff = DiffParser.Parse(rawDiff);
    var result = await orchestrator.RunAsync(diff);

    var firedRuleIds = result.Findings.Select(f => f.RuleId).Distinct().ToHashSet();
    var expectedIds = entry.ExpectedGciRules.Select(NormalizeRuleId).ToList();

    if (entry.ExpectedOutcome == "fire")
    {
        if (expectedIds.Count > 0)
        {
            foreach (var ruleId in expectedIds)
            {
                EnsureRule(ruleAccumulators, ruleId);
                if (firedRuleIds.Contains(ruleId))
                {
                    ruleAccumulators[ruleId] = ruleAccumulators[ruleId] with { Tp = ruleAccumulators[ruleId].Tp + 1 };
                    aggTp++;
                }
                else
                {
                    ruleAccumulators[ruleId] = ruleAccumulators[ruleId] with { Fn = ruleAccumulators[ruleId].Fn + 1 };
                    aggFn++;
                }
            }
        }
        else
        {
            // no specific rule expected — count as aggregate TP/FN
            if (result.HasFindings) aggTp++;
            else aggFn++;
        }
    }
    else if (entry.ExpectedOutcome == "do-not-fire")
    {
        if (expectedIds.Count > 0)
        {
            foreach (var ruleId in expectedIds)
            {
                EnsureRule(ruleAccumulators, ruleId);
                if (firedRuleIds.Contains(ruleId))
                {
                    ruleAccumulators[ruleId] = ruleAccumulators[ruleId] with { Fp = ruleAccumulators[ruleId].Fp + 1 };
                    aggFp++;
                }
                else
                {
                    ruleAccumulators[ruleId] = ruleAccumulators[ruleId] with { Tn = ruleAccumulators[ruleId].Tn + 1 };
                    aggTn++;
                }
            }
        }
        else
        {
            if (result.HasFindings) aggFp++;
            else aggTn++;
        }
    }
    // edge-case entries skipped
}

// Build per-rule stats
var ruleStatsList = ruleAccumulators
    .OrderBy(kv => kv.Key)
    .Select(kv =>
    {
        var (tp, fp, fn, tn, desc) = kv.Value;
        return new RuleStats
        {
            RuleId = kv.Key,
            Description = desc,
            Tp = tp, Fp = fp, Fn = fn, Tn = tn,
            Precision = CalcPrecision(tp, fp),
            Recall = CalcRecall(tp, fn),
            F1 = CalcF1(tp, fp, fn)
        };
    }).ToList();

var aggregate = new AggregateStats
{
    TotalFixtures = allEntries.Count,
    Tp = aggTp, Fp = aggFp, Fn = aggFn, Tn = aggTn,
    Precision = CalcPrecision(aggTp, aggFp),
    Recall = CalcRecall(aggTp, aggFn),
    F1 = CalcF1(aggTp, aggFp, aggFn)
};

var report = new BenchmarkReport
{
    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
    Aggregate = aggregate,
    Rules = ruleStatsList
};

// Write JSON
var jsonPath = Path.Combine(outputDir, "latest.json");
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOpts));

// Write CSV
var csvPath = Path.Combine(outputDir, "latest.csv");
var csvLines = new List<string> { "rule_id,description,tp,fp,fn,tn,precision,recall,f1" };
csvLines.AddRange(ruleStatsList.Select(r =>
    $"{r.RuleId},{EscapeCsv(r.Description)},{r.Tp},{r.Fp},{r.Fn},{r.Tn},{r.Precision:F4},{r.Recall:F4},{r.F1:F4}"));
await File.WriteAllLinesAsync(csvPath, csvLines);

// Print summary table
Console.WriteLine();
Console.WriteLine($"{"Rule",-12} {"TP",5} {"FP",5} {"FN",5} {"TN",5} {"Prec",8} {"Recall",8} {"F1",8}");
Console.WriteLine(new string('-', 70));
foreach (var r in ruleStatsList)
    Console.WriteLine($"{r.RuleId,-12} {r.Tp,5} {r.Fp,5} {r.Fn,5} {r.Tn,5} {r.Precision,8:F4} {r.Recall,8:F4} {r.F1,8:F4}");
Console.WriteLine(new string('-', 70));
Console.WriteLine($"{"AGGREGATE",-12} {aggregate.Tp,5} {aggregate.Fp,5} {aggregate.Fn,5} {aggregate.Tn,5} {aggregate.Precision,8:F4} {aggregate.Recall,8:F4} {aggregate.F1,8:F4}");
Console.WriteLine();
Console.WriteLine($"Total fixtures: {aggregate.TotalFixtures}");
Console.WriteLine($"Output: {jsonPath}");
Console.WriteLine($"        {csvPath}");

return 0;

static string NormalizeRuleId(string id)
{
    if (id.StartsWith("GCI", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(id[3..], out int n))
        return $"GCI{n:D4}";
    return id;
}

static void EnsureRule(Dictionary<string, (int Tp, int Fp, int Fn, int Tn, string Desc)> dict, string ruleId)
{
    if (!dict.ContainsKey(ruleId))
        dict[ruleId] = (0, 0, 0, 0, string.Empty);
}

static double CalcPrecision(int tp, int fp) =>
    (tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp);

static double CalcRecall(int tp, int fn) =>
    (tp + fn) == 0 ? 0.0 : (double)tp / (tp + fn);

static double CalcF1(int tp, int fp, int fn)
{
    var p = CalcPrecision(tp, fp);
    var r = CalcRecall(tp, fn);
    return (p + r) == 0 ? 0.0 : 2 * p * r / (p + r);
}

static string EscapeCsv(string s)
{
    if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        return $"\"{s.Replace("\"", "\"\"")}\"";
    return s;
}

// Local types to avoid dependency on Benchmarks project
namespace GauntletCI.BenchmarkReporter
{
    internal class FixtureManifest
    {
        [JsonPropertyName("mapped_gci_rules")]
        public List<string> MappedGciRules { get; set; } = [];

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("fixtures")]
        public List<FixtureEntry> Fixtures { get; set; } = [];
    }

    internal class FixtureEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("diff_file")]
        public string DiffFile { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("expected_outcome")]
        public string ExpectedOutcome { get; set; } = string.Empty;

        [JsonPropertyName("expected_gci_rules")]
        public List<string> ExpectedGciRules { get; set; } = [];

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("origin")]
        public string Origin { get; set; } = string.Empty;

        [JsonPropertyName("source_url")]
        public string SourceUrl { get; set; } = string.Empty;
    }
}
