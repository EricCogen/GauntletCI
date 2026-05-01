// SPDX-License-Identifier: Elastic-2.0
using Microsoft.Data.Sqlite;

namespace GauntletCI.Corpus.Analysis;

/// <summary>
/// Analyzes the Corpus of Failure to identify rule refinement opportunities.
/// Provides FP/FN breakdown, pattern clustering, and automated test case generation.
/// </summary>
public class CorpusAnalyzer
{
    private readonly string _dbPath;

    public CorpusAnalyzer(string dbPath = "./data/gauntletci-corpus.db")
    {
        _dbPath = dbPath;
    }

    /// <summary>
    /// Gets the FP/FN breakdown for a specific rule from the corpus.
    /// </summary>
    public async Task<RuleCorpusMetrics> AnalyzeRuleAsync(string ruleId)
    {
        if (!File.Exists(_dbPath))
            throw new FileNotFoundException($"Corpus database not found at {_dbPath}");

        var metrics = new RuleCorpusMetrics { RuleId = ruleId };

        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            // Query: Find all corpus hits for this rule
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT 
                    finding_id,
                    pr_url,
                    commit_sha,
                    file_path,
                    summary,
                    label,
                    evidence
                FROM corpus_findings
                WHERE rule_id = @ruleId
                ORDER BY label DESC
                """;
            cmd.Parameters.AddWithValue("@ruleId", ruleId);

            using var reader = await cmd.ExecuteReaderAsync();
            var falsePositives = new List<CorpusFinding>();
            var truePositives = new List<CorpusFinding>();
            var falseNegatives = new List<CorpusFinding>();
            var unknowns = new List<CorpusFinding>();

            while (await reader.ReadAsync())
            {
                var finding = new CorpusFinding
                {
                    FindingId = reader.GetString(0),
                    PrUrl = reader.GetString(1),
                    CommitSha = reader.GetString(2),
                    FilePath = reader.GetString(3),
                    Summary = reader.GetString(4),
                    Evidence = reader.GetString(6)
                };

                var label = reader.GetString(5);
                switch (label?.ToLower())
                {
                    case "fp":
                        falsePositives.Add(finding);
                        break;
                    case "tp":
                        truePositives.Add(finding);
                        break;
                    case "fn":
                        falseNegatives.Add(finding);
                        break;
                    default:
                        unknowns.Add(finding);
                        break;
                }
            }

            metrics.TruePositives = truePositives;
            metrics.FalsePositives = falsePositives;
            metrics.FalseNegatives = falseNegatives;
            metrics.Unknowns = unknowns;
            metrics.Precision = truePositives.Count > 0 
                ? (double)truePositives.Count / (truePositives.Count + falsePositives.Count)
                : 0;
            metrics.Recall = (truePositives.Count + falseNegatives.Count) > 0
                ? (double)truePositives.Count / (truePositives.Count + falseNegatives.Count)
                : 0;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Failed to analyze corpus for {ruleId}: table may not exist", ex);
        }

        return metrics;
    }

    /// <summary>
    /// Groups false positives by pattern to identify over-broad detection logic.
    /// </summary>
    public PatternCluster ClusterFalsePositives(RuleCorpusMetrics metrics)
    {
        var cluster = new PatternCluster { RuleId = metrics.RuleId };

        var fpByPattern = new Dictionary<string, List<CorpusFinding>>();
        foreach (var fp in metrics.FalsePositives)
        {
            var pattern = ExtractPattern(fp.Evidence);
            if (!fpByPattern.ContainsKey(pattern))
                fpByPattern[pattern] = [];
            fpByPattern[pattern].Add(fp);
        }

        cluster.Patterns = fpByPattern
            .OrderByDescending(x => x.Value.Count)
            .Select(x => new PatternGroup 
            { 
                Pattern = x.Key, 
                Count = x.Value.Count,
                Examples = x.Value.Take(3).ToList()
            })
            .ToList();

        return cluster;
    }

    /// <summary>
    /// Groups false negatives by context to identify missing detection cases.
    /// </summary>
    public PatternCluster ClusterFalseNegatives(RuleCorpusMetrics metrics)
    {
        var cluster = new PatternCluster { RuleId = metrics.RuleId };

        var fnByContext = new Dictionary<string, List<CorpusFinding>>();
        foreach (var fn in metrics.FalseNegatives)
        {
            var context = ExtractContext(fn.Evidence);
            if (!fnByContext.ContainsKey(context))
                fnByContext[context] = [];
            fnByContext[context].Add(fn);
        }

        cluster.Patterns = fnByContext
            .OrderByDescending(x => x.Value.Count)
            .Select(x => new PatternGroup 
            { 
                Pattern = x.Key, 
                Count = x.Value.Count,
                Examples = x.Value.Take(3).ToList()
            })
            .ToList();

        return cluster;
    }

    /// <summary>
    /// Generates test cases from corpus findings to improve coverage.
    /// </summary>
    public List<GeneratedTestCase> GenerateTestCases(RuleCorpusMetrics metrics, int maxPerCategory = 3)
    {
        var testCases = new List<GeneratedTestCase>();

        var fpTests = metrics.FalsePositives
            .Take(maxPerCategory)
            .Select(fp => new GeneratedTestCase
            {
                Name = $"{metrics.RuleId}_SafePattern_{fp.FindingId}",
                Type = "FalsePositiveAvoidance",
                Evidence = fp.Evidence,
                ExpectedFinding = false,
                Source = fp.PrUrl
            })
            .ToList();
        testCases.AddRange(fpTests);

        var fnTests = metrics.FalseNegatives
            .Take(maxPerCategory)
            .Select(fn => new GeneratedTestCase
            {
                Name = $"{metrics.RuleId}_MissingCase_{fn.FindingId}",
                Type = "FalseNegativeRecovery",
                Evidence = fn.Evidence,
                ExpectedFinding = true,
                Source = fn.PrUrl
            })
            .ToList();
        testCases.AddRange(fnTests);

        return testCases;
    }

    /// <summary>
    /// Generates a refinement report recommending specific fixes.
    /// </summary>
    public RefinementReport GenerateRefinementReport(RuleCorpusMetrics metrics)
    {
        var report = new RefinementReport { RuleId = metrics.RuleId };

        if (metrics.Precision < 0.8)
        {
            report.Priority = "HIGH";
            report.Issues.Add(new RefinementIssue 
            { 
                Type = "HighFalsePositiveRate",
                Message = $"Precision {metrics.Precision:P} is below 80%. Rule flags safe code too frequently.",
                Recommendation = "Tighten detection logic with additional context guards."
            });
        }

        if (metrics.Recall < 0.6)
        {
            report.Priority = "HIGH";
            report.Issues.Add(new RefinementIssue 
            { 
                Type = "LowRecall",
                Message = $"Recall {metrics.Recall:P} is below 60%. Rule misses {metrics.FalseNegatives.Count} risky patterns.",
                Recommendation = "Expand detection patterns to catch missing cases."
            });
        }

        if (metrics.FalsePositives.Count == 0 && metrics.TruePositives.Count == 0)
        {
            report.Priority = "CRITICAL";
            report.Issues.Add(new RefinementIssue 
            { 
                Type = "NoSignal",
                Message = "Rule has no corpus signal (0 TPs, 0 FPs). Rule may be dead or too restrictive.",
                Recommendation = "Audit rule logic. Consider deprecation if orphaned."
            });
        }

        return report;
    }

    private static string ExtractPattern(string evidence)
    {
        var lines = evidence.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 
            ? lines[0].Length > 50 
                ? lines[0][..50] + "..." 
                : lines[0]
            : "unknown";
    }

    private static string ExtractContext(string evidence)
    {
        if (evidence.Contains("async"))
            return "async-context";
        if (evidence.Contains("IDisposable"))
            return "disposable-context";
        if (evidence.Contains("lock"))
            return "synchronization-context";
        if (evidence.Contains("try"))
            return "exception-handling";
        if (evidence.Contains("SQL"))
            return "database-context";

        return "other-context";
    }
}

public class RuleCorpusMetrics
{
    public string RuleId { get; set; } = "";
    public List<CorpusFinding> TruePositives { get; set; } = [];
    public List<CorpusFinding> FalsePositives { get; set; } = [];
    public List<CorpusFinding> FalseNegatives { get; set; } = [];
    public List<CorpusFinding> Unknowns { get; set; } = [];
    public double Precision { get; set; }
    public double Recall { get; set; }
}

public class CorpusFinding
{
    public string FindingId { get; set; } = "";
    public string PrUrl { get; set; } = "";
    public string CommitSha { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Evidence { get; set; } = "";
}

public class PatternCluster
{
    public string RuleId { get; set; } = "";
    public List<PatternGroup> Patterns { get; set; } = [];
}

public class PatternGroup
{
    public string Pattern { get; set; } = "";
    public int Count { get; set; }
    public List<CorpusFinding> Examples { get; set; } = [];
}

public class GeneratedTestCase
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Evidence { get; set; } = "";
    public bool ExpectedFinding { get; set; }
    public string Source { get; set; } = "";
}

public class RefinementReport
{
    public string RuleId { get; set; } = "";
    public string Priority { get; set; } = "NORMAL";
    public List<RefinementIssue> Issues { get; set; } = [];
}

public class RefinementIssue
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string Recommendation { get; set; } = "";
}
