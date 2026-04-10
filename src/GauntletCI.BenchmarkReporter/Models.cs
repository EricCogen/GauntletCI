// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.BenchmarkReporter;

public class BenchmarkReport
{
    public string Timestamp { get; set; } = string.Empty;
    public AggregateStats Aggregate { get; set; } = new();
    public List<RuleStats> Rules { get; set; } = [];
}

public class RuleStats
{
    public string RuleId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Tp { get; set; }
    public int Fp { get; set; }
    public int Fn { get; set; }
    public int Tn { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}

public class AggregateStats
{
    public int TotalFixtures { get; set; }
    public int Tp { get; set; }
    public int Fp { get; set; }
    public int Fn { get; set; }
    public int Tn { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}
