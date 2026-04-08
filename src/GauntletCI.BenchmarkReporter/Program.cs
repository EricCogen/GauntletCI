// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

return Run(args);

static int Run(string[] args)
{
    try
    {
        ReporterOptions options = ReporterOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine(ReporterOptions.HelpText);
            return 0;
        }

        BenchmarkReport report = BenchmarkReportBuilder.Build(options);
        Directory.CreateDirectory(options.OutputDirectory);

        string jsonPath = Path.Combine(options.OutputDirectory, "latest.json");
        string csvPath = Path.Combine(options.OutputDirectory, "latest.csv");

        JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions));
        File.WriteAllText(csvPath, CsvFormatter.ToCsv(report.Fixtures));

        Console.WriteLine($"Benchmark report written:");
        Console.WriteLine($"- {jsonPath}");
        Console.WriteLine($"- {csvPath}");
        Console.WriteLine($"Fixtures evaluated: {report.Corpus.FixturesEvaluated}");
        Console.WriteLine($"Overall precision: {FormatMetric(report.Summary.Precision)}");
        Console.WriteLine($"Overall recall: {FormatMetric(report.Summary.Recall)}");
        Console.WriteLine($"Overall pass rate: {FormatMetric(report.Summary.PassRate)}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Benchmark reporter failed: {ex.Message}");
        return 1;
    }
}

static string FormatMetric(double? value)
{
    return value is null
        ? "n/a"
        : value.Value.ToString("P2", CultureInfo.InvariantCulture);
}

internal sealed record ReporterOptions(
    string RepoRoot,
    string FixturesRoot,
    string OutputDirectory,
    bool IncludeSynthetic,
    bool ShowHelp)
{
    public static string HelpText =>
        """
gauntletci benchmark reporter

Usage:
  dotnet run --project src/GauntletCI.BenchmarkReporter -- [options]

Options:
  --repo-root <path>         Repository root (default: walks up from CWD to find repo root)
  --fixtures-root <path>     Curated fixtures root (default: <repo-root>/tests/GauntletCI.Benchmarks/Fixtures/curated)
  --output-dir <path>        Output directory (default: docs/benchmarks)
  --include-synthetic        Include synthetic fixtures in metrics (default: false)
  --help, -h                 Show help
""";

    public static ReporterOptions Parse(string[] args)
    {
        string repoRoot = ResolveDefaultRepoRoot();
        string? fixturesRoot = null;
        string? outputDirectory = null;
        bool includeSynthetic = false;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--repo-root" when i + 1 < args.Length:
                    repoRoot = Path.GetFullPath(args[++i]);
                    break;
                case "--fixtures-root" when i + 1 < args.Length:
                    fixturesRoot = Path.GetFullPath(args[++i]);
                    break;
                case "--output-dir" when i + 1 < args.Length:
                    outputDirectory = Path.GetFullPath(args[++i]);
                    break;
                case "--include-synthetic":
                    includeSynthetic = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    showHelp = true;
                    break;
            }
        }

        fixturesRoot ??= Path.Combine(repoRoot, "tests", "GauntletCI.Benchmarks", "Fixtures", "curated");
        outputDirectory ??= Path.Combine(repoRoot, "docs", "benchmarks");
        return new ReporterOptions(repoRoot, fixturesRoot, outputDirectory, includeSynthetic, showHelp);
    }

    private static string ResolveDefaultRepoRoot()
    {
        string currentDirectory = Directory.GetCurrentDirectory();

        for (DirectoryInfo? directory = new DirectoryInfo(currentDirectory); directory is not null; directory = directory.Parent)
        {
            if (LooksLikeRepositoryRoot(directory.FullName))
            {
                return directory.FullName;
            }
        }

        return currentDirectory;
    }

    private static bool LooksLikeRepositoryRoot(string path)
    {
        return Directory.Exists(Path.Combine(path, "src"))
            && Directory.Exists(Path.Combine(path, "tests"));
    }
}

internal static class BenchmarkReportBuilder
{
    public static BenchmarkReport Build(ReporterOptions options)
    {
        if (!Directory.Exists(options.FixturesRoot))
        {
            throw new DirectoryNotFoundException($"Fixtures root was not found: {options.FixturesRoot}");
        }

        DeterministicAnalysisRunner runner = new();
        DiffContextTrimmer trimmer = new();

        List<FixtureEvaluationRecord> fixtureResults = [];
        foreach (string fixtureSetDirectory in Directory.EnumerateDirectories(options.FixturesRoot).OrderBy(static d => d, StringComparer.OrdinalIgnoreCase))
        {
            string fixtureSetName = Path.GetFileName(fixtureSetDirectory);
            string manifestPath = Path.Combine(fixtureSetDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string manifestJson = File.ReadAllText(manifestPath);
            BenchmarkManifest? manifest = JsonSerializer.Deserialize<BenchmarkManifest>(manifestJson, ManifestJsonOptions);
            if (manifest is null)
            {
                throw new InvalidOperationException($"Unable to parse benchmark manifest: {manifestPath}");
            }

            IReadOnlyList<string> mappedRules = DistinctRules(manifest.MappedGciRules);
            foreach (BenchmarkFixture fixture in manifest.Fixtures)
            {
                if (!options.IncludeSynthetic && fixture.IsSynthetic)
                {
                    continue;
                }

                string diffPath = Path.Combine(fixtureSetDirectory, fixture.DiffFile);
                if (!File.Exists(diffPath))
                {
                    throw new FileNotFoundException($"Benchmark diff file not found: {diffPath}");
                }

                string rawDiff = File.ReadAllText(diffPath);
                string strippedDiff = StripFixtureHeader(rawDiff);
                DiffMetadata metadata = trimmer.Trim(strippedDiff, maxDiffTokens: 8000).Metadata;
                IReadOnlyList<Finding> findings = runner.Analyze(strippedDiff, metadata);
                IReadOnlyList<string> firedRules = DistinctRules(findings.Select(static finding => finding.RuleId));
                IReadOnlyList<string> expectedRules = DistinctRules(fixture.ExpectedGciRules);

                bool expectedRuleFired = expectedRules.Any(rule => ContainsRule(firedRules, rule));
                IReadOnlyList<string> firedMappedRules = firedRules.Where(rule => ContainsRule(mappedRules, rule)).ToArray();
                bool unexpectedMappedRuleFired = firedMappedRules.Count > 0;
                bool passed = fixture.ShouldFire ? expectedRuleFired : !unexpectedMappedRuleFired;
                string classification = Classify(fixture.ShouldFire, passed);

                IReadOnlyList<string> missingExpectedRules = fixture.ShouldFire
                    ? expectedRules.Where(rule => !ContainsRule(firedRules, rule)).ToArray()
                    : [];
                IReadOnlyList<string> unexpectedMappedRules = !fixture.ShouldFire
                    ? firedMappedRules
                    : [];

                Finding? representativeFinding = SelectRepresentativeFinding(findings, expectedRules);
                fixtureResults.Add(new FixtureEvaluationRecord(
                    FixtureSet: fixtureSetName,
                    FixtureId: fixture.Id,
                    Origin: fixture.Origin,
                    IsSynthetic: fixture.IsSynthetic,
                    SourceUrl: fixture.SourceUrl,
                    ShouldFire: fixture.ShouldFire,
                    ExpectedOutcome: fixture.ExpectedOutcome,
                    Passed: passed,
                    Classification: classification,
                    ExpectedRules: expectedRules,
                    MappedRules: mappedRules,
                    FiredRules: firedRules,
                    MissingExpectedRules: missingExpectedRules,
                    UnexpectedMappedRules: unexpectedMappedRules,
                    FindingsCount: findings.Count,
                    HighSeverityCount: findings.Count(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)),
                    MediumSeverityCount: findings.Count(static finding => finding.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)),
                    LowSeverityCount: findings.Count(static finding => finding.Severity.Equals("low", StringComparison.OrdinalIgnoreCase)),
                    Notes: fixture.Notes,
                    SampleFinding: representativeFinding?.FindingText,
                    SampleEvidence: representativeFinding?.Evidence));
            }
        }

        MetricsSummary summary = MetricsSummary.Build(fixtureResults);
        IReadOnlyList<RuleMetricsRecord> perRule = BuildPerRuleMetrics(fixtureResults);
        IReadOnlyList<CaseStudyRecord> caseStudies = BuildCaseStudies(fixtureResults);

        int totalFixtures = fixtureResults.Count;
        int realFixtures = fixtureResults.Count(static fixture => !fixture.IsSynthetic);
        int syntheticFixtures = fixtureResults.Count(static fixture => fixture.IsSynthetic);

        return new BenchmarkReport(
            GeneratedUtc: DateTimeOffset.UtcNow,
            RepositoryCommit: TryGetGitCommit(options.RepoRoot),
            Analyzer: new AnalyzerRecord(
                Name: "deterministic",
                Source: "GauntletCI.Core.Evaluation.DeterministicAnalysisRunner",
                Version: "v1"),
            Corpus: new CorpusRecord(
                FixturesEvaluated: totalFixtures,
                RealFixtures: realFixtures,
                SyntheticFixtures: syntheticFixtures,
                IncludeSynthetic: options.IncludeSynthetic),
            Summary: summary,
            PerRule: perRule,
            CaseStudies: caseStudies,
            Fixtures: fixtureResults);
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static IReadOnlyList<string> DistinctRules(IEnumerable<string> rules)
    {
        return rules
            .Where(static rule => !string.IsNullOrWhiteSpace(rule))
            .Select(static rule => rule.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string StripFixtureHeader(string diffContent)
    {
        IEnumerable<string> lines = diffContent
            .Split('\n')
            .SkipWhile(static line => line.TrimStart().StartsWith('#') || string.IsNullOrWhiteSpace(line));
        return string.Join('\n', lines);
    }

    private static bool ContainsRule(IReadOnlyList<string> rules, string candidate)
    {
        return rules.Any(rule => string.Equals(rule, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string Classify(bool shouldFire, bool passed)
    {
        if (shouldFire)
        {
            return passed ? "tp" : "fn";
        }

        return passed ? "tn" : "fp";
    }

    private static Finding? SelectRepresentativeFinding(IReadOnlyList<Finding> findings, IReadOnlyList<string> expectedRules)
    {
        foreach (string expectedRule in expectedRules)
        {
            Finding? match = findings.FirstOrDefault(finding => string.Equals(finding.RuleId, expectedRule, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return findings.FirstOrDefault();
    }

    private static IReadOnlyList<RuleMetricsRecord> BuildPerRuleMetrics(IReadOnlyList<FixtureEvaluationRecord> fixtures)
    {
        IReadOnlyList<string> rules = fixtures
            .SelectMany(static fixture => fixture.MappedRules)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<RuleMetricsRecord> result = [];
        foreach (string rule in rules)
        {
            IReadOnlyList<FixtureEvaluationRecord> scope = fixtures
                .Where(fixture => ContainsRule(fixture.MappedRules, rule))
                .ToArray();

            int tp = 0;
            int tn = 0;
            int fp = 0;
            int fn = 0;
            foreach (FixtureEvaluationRecord fixture in scope)
            {
                bool expectedPositive = fixture.ShouldFire && ContainsRule(fixture.ExpectedRules, rule);
                bool predictedPositive = ContainsRule(fixture.FiredRules, rule);

                if (expectedPositive && predictedPositive)
                {
                    tp++;
                }
                else if (!expectedPositive && !predictedPositive)
                {
                    tn++;
                }
                else if (!expectedPositive && predictedPositive)
                {
                    fp++;
                }
                else
                {
                    fn++;
                }
            }

            result.Add(new RuleMetricsRecord(
                RuleId: rule,
                FixturesInScope: scope.Count,
                TruePositive: tp,
                TrueNegative: tn,
                FalsePositive: fp,
                FalseNegative: fn,
                Precision: SafeDivide(tp, tp + fp),
                Recall: SafeDivide(tp, tp + fn),
                Accuracy: SafeDivide(tp + tn, tp + tn + fp + fn),
                FalsePositiveRate: SafeDivide(fp, fp + tn),
                FalseNegativeRate: SafeDivide(fn, fn + tp)));
        }

        return result;
    }

    private static IReadOnlyList<CaseStudyRecord> BuildCaseStudies(IReadOnlyList<FixtureEvaluationRecord> fixtures)
    {
        return fixtures
            .Where(static fixture =>
                !fixture.IsSynthetic &&
                fixture.ShouldFire &&
                fixture.Passed &&
                !string.IsNullOrWhiteSpace(fixture.SourceUrl) &&
                !string.IsNullOrWhiteSpace(fixture.SampleFinding) &&
                !string.IsNullOrWhiteSpace(fixture.SampleEvidence))
            .OrderByDescending(static fixture => fixture.FindingsCount)
            .ThenBy(static fixture => fixture.FixtureSet, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static fixture => fixture.FixtureId, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(fixture => new CaseStudyRecord(
                FixtureSet: fixture.FixtureSet,
                FixtureId: fixture.FixtureId,
                RuleId: fixture.ExpectedRules.FirstOrDefault() ?? fixture.FiredRules.FirstOrDefault() ?? "unknown",
                SourceUrl: fixture.SourceUrl!,
                WhyItMatters: fixture.Notes,
                SampleFinding: fixture.SampleFinding!,
                SampleEvidence: fixture.SampleEvidence!))
            .ToArray();
    }

    private static string? TryGetGitCommit(string repoRoot)
    {
        try
        {
            ProcessStartInfo info = new("git", "rev-parse HEAD")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = Process.Start(info)!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static double? SafeDivide(int numerator, int denominator)
    {
        return denominator == 0 ? null : (double)numerator / denominator;
    }
}

internal static class CsvFormatter
{
    public static string ToCsv(IReadOnlyList<FixtureEvaluationRecord> rows)
    {
        StringBuilder builder = new();
        builder.AppendLine("fixture_set,fixture_id,origin,is_synthetic,expected_outcome,classification,passed,expected_rules,fired_rules,missing_expected_rules,unexpected_mapped_rules,findings_count,high_count,medium_count,low_count,source_url");

        foreach (FixtureEvaluationRecord row in rows)
        {
            builder.Append(Escape(row.FixtureSet)).Append(',')
                .Append(Escape(row.FixtureId)).Append(',')
                .Append(Escape(row.Origin)).Append(',')
                .Append(row.IsSynthetic ? "true" : "false").Append(',')
                .Append(Escape(row.ExpectedOutcome)).Append(',')
                .Append(Escape(row.Classification)).Append(',')
                .Append(row.Passed ? "true" : "false").Append(',')
                .Append(Escape(string.Join('|', row.ExpectedRules))).Append(',')
                .Append(Escape(string.Join('|', row.FiredRules))).Append(',')
                .Append(Escape(string.Join('|', row.MissingExpectedRules))).Append(',')
                .Append(Escape(string.Join('|', row.UnexpectedMappedRules))).Append(',')
                .Append(row.FindingsCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.HighSeverityCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.MediumSeverityCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.LowSeverityCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(row.SourceUrl ?? string.Empty))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        string escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}

internal sealed record BenchmarkReport(
    [property: JsonPropertyName("generated_utc")] DateTimeOffset GeneratedUtc,
    [property: JsonPropertyName("repository_commit")] string? RepositoryCommit,
    [property: JsonPropertyName("analyzer")] AnalyzerRecord Analyzer,
    [property: JsonPropertyName("corpus")] CorpusRecord Corpus,
    [property: JsonPropertyName("summary")] MetricsSummary Summary,
    [property: JsonPropertyName("per_rule")] IReadOnlyList<RuleMetricsRecord> PerRule,
    [property: JsonPropertyName("case_studies")] IReadOnlyList<CaseStudyRecord> CaseStudies,
    [property: JsonPropertyName("fixtures")] IReadOnlyList<FixtureEvaluationRecord> Fixtures);

internal sealed record AnalyzerRecord(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("version")] string Version);

internal sealed record CorpusRecord(
    [property: JsonPropertyName("fixtures_evaluated")] int FixturesEvaluated,
    [property: JsonPropertyName("real_fixtures")] int RealFixtures,
    [property: JsonPropertyName("synthetic_fixtures")] int SyntheticFixtures,
    [property: JsonPropertyName("include_synthetic")] bool IncludeSynthetic);

internal sealed record MetricsSummary(
    [property: JsonPropertyName("true_positive")] int TruePositive,
    [property: JsonPropertyName("true_negative")] int TrueNegative,
    [property: JsonPropertyName("false_positive")] int FalsePositive,
    [property: JsonPropertyName("false_negative")] int FalseNegative,
    [property: JsonPropertyName("fixtures_passed")] int FixturesPassed,
    [property: JsonPropertyName("fixtures_failed")] int FixturesFailed,
    [property: JsonPropertyName("precision")] double? Precision,
    [property: JsonPropertyName("recall")] double? Recall,
    [property: JsonPropertyName("accuracy")] double? Accuracy,
    [property: JsonPropertyName("false_positive_rate")] double? FalsePositiveRate,
    [property: JsonPropertyName("false_negative_rate")] double? FalseNegativeRate,
    [property: JsonPropertyName("pass_rate")] double? PassRate)
{
    public static MetricsSummary Build(IReadOnlyList<FixtureEvaluationRecord> fixtures)
    {
        int tp = fixtures.Count(static fixture => fixture.Classification == "tp");
        int tn = fixtures.Count(static fixture => fixture.Classification == "tn");
        int fp = fixtures.Count(static fixture => fixture.Classification == "fp");
        int fn = fixtures.Count(static fixture => fixture.Classification == "fn");
        int passed = fixtures.Count(static fixture => fixture.Passed);
        int failed = fixtures.Count - passed;
        int total = fixtures.Count;

        return new MetricsSummary(
            TruePositive: tp,
            TrueNegative: tn,
            FalsePositive: fp,
            FalseNegative: fn,
            FixturesPassed: passed,
            FixturesFailed: failed,
            Precision: SafeDivide(tp, tp + fp),
            Recall: SafeDivide(tp, tp + fn),
            Accuracy: SafeDivide(tp + tn, total),
            FalsePositiveRate: SafeDivide(fp, fp + tn),
            FalseNegativeRate: SafeDivide(fn, fn + tp),
            PassRate: SafeDivide(passed, total));
    }

    private static double? SafeDivide(int numerator, int denominator)
    {
        return denominator == 0 ? null : (double)numerator / denominator;
    }
}

internal sealed record RuleMetricsRecord(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("fixtures_in_scope")] int FixturesInScope,
    [property: JsonPropertyName("true_positive")] int TruePositive,
    [property: JsonPropertyName("true_negative")] int TrueNegative,
    [property: JsonPropertyName("false_positive")] int FalsePositive,
    [property: JsonPropertyName("false_negative")] int FalseNegative,
    [property: JsonPropertyName("precision")] double? Precision,
    [property: JsonPropertyName("recall")] double? Recall,
    [property: JsonPropertyName("accuracy")] double? Accuracy,
    [property: JsonPropertyName("false_positive_rate")] double? FalsePositiveRate,
    [property: JsonPropertyName("false_negative_rate")] double? FalseNegativeRate);

internal sealed record CaseStudyRecord(
    [property: JsonPropertyName("fixture_set")] string FixtureSet,
    [property: JsonPropertyName("fixture_id")] string FixtureId,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("source_url")] string SourceUrl,
    [property: JsonPropertyName("why_it_matters")] string WhyItMatters,
    [property: JsonPropertyName("sample_finding")] string SampleFinding,
    [property: JsonPropertyName("sample_evidence")] string SampleEvidence);

internal sealed record FixtureEvaluationRecord(
    [property: JsonPropertyName("fixture_set")] string FixtureSet,
    [property: JsonPropertyName("fixture_id")] string FixtureId,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("is_synthetic")] bool IsSynthetic,
    [property: JsonPropertyName("source_url")] string? SourceUrl,
    [property: JsonPropertyName("should_fire")] bool ShouldFire,
    [property: JsonPropertyName("expected_outcome")] string ExpectedOutcome,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("expected_rules")] IReadOnlyList<string> ExpectedRules,
    [property: JsonPropertyName("mapped_rules")] IReadOnlyList<string> MappedRules,
    [property: JsonPropertyName("fired_rules")] IReadOnlyList<string> FiredRules,
    [property: JsonPropertyName("missing_expected_rules")] IReadOnlyList<string> MissingExpectedRules,
    [property: JsonPropertyName("unexpected_mapped_rules")] IReadOnlyList<string> UnexpectedMappedRules,
    [property: JsonPropertyName("findings_count")] int FindingsCount,
    [property: JsonPropertyName("high_severity_count")] int HighSeverityCount,
    [property: JsonPropertyName("medium_severity_count")] int MediumSeverityCount,
    [property: JsonPropertyName("low_severity_count")] int LowSeverityCount,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("sample_finding")] string? SampleFinding,
    [property: JsonPropertyName("sample_evidence")] string? SampleEvidence);

internal sealed record BenchmarkManifest(
    [property: JsonPropertyName("mapped_gci_rules")] IReadOnlyList<string> MappedGciRules,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fixtures")] IReadOnlyList<BenchmarkFixture> Fixtures);

internal sealed record BenchmarkFixture(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("diff_file")] string DiffFile,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("expected_outcome")] string ExpectedOutcome,
    [property: JsonPropertyName("expected_gci_rules")] IReadOnlyList<string> ExpectedGciRules,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("origin")] string Origin = "synthetic",
    [property: JsonPropertyName("source_url")] string? SourceUrl = null)
{
    public bool ShouldFire => string.Equals(ExpectedOutcome, "fire", StringComparison.OrdinalIgnoreCase);
    public bool IsSynthetic => string.Equals(Origin, "synthetic", StringComparison.OrdinalIgnoreCase);
}
