// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text;
using System.Text.Json;
using GauntletCI.Cli.Audit;

namespace GauntletCI.Cli.Commands;

public static class AuditCommand
{
    public static Command Create()
    {
        var cmd = new Command("audit", "Manage the local audit trail of GauntletCI scans");
        cmd.AddCommand(CreateExport());
        cmd.AddCommand(CreateStats());
        return cmd;
    }

    // ── export ────────────────────────────────────────────────────────────────

    private static Command CreateExport()
    {
        var formatOption = new Option<string>(
            "--format",
            () => "json",
            "Output format: json or csv");

        var outputOption = new Option<FileInfo?>(
            "--output",
            "Write to file instead of stdout");

        var sinceOption = new Option<DateTimeOffset?>(
            "--since",
            "Include scans on or after this date (ISO 8601, e.g. 2025-01-01)");

        var lastOption = new Option<int?>(
            "--last",
            "Include only the last N scans");

        var cmd = new Command("export", "Export the audit log as JSON or CSV")
        {
            formatOption,
            outputOption,
            sinceOption,
            lastOption,
        };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var outputFile = ctx.ParseResult.GetValueForOption(outputOption);
            var since = ctx.ParseResult.GetValueForOption(sinceOption);
            var last = ctx.ParseResult.GetValueForOption(lastOption);

            var entries = await AuditLog.LoadAllAsync(ctx.GetCancellationToken());

            // Apply filters
            if (since.HasValue)
                entries = [.. entries.Where(e => e.Timestamp >= since.Value)];
            if (last.HasValue)
                entries = [.. entries.TakeLast(last.Value)];

            if (entries.Count == 0)
            {
                Console.Error.WriteLine("[audit] No scan records found matching the given filters.");
                ctx.ExitCode = 0;
                return;
            }

            string output;
            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
                output = ToCsv(entries);
            else
                output = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });

            if (outputFile is not null)
            {
                Directory.CreateDirectory(outputFile.DirectoryName!);
                await File.WriteAllTextAsync(outputFile.FullName, output, ctx.GetCancellationToken());
                Console.WriteLine($"[audit] Exported {entries.Count} scan(s) → {outputFile.FullName}");
            }
            else
            {
                Console.WriteLine(output);
            }
        });

        return cmd;
    }

    // ── stats ─────────────────────────────────────────────────────────────────

    private static Command CreateStats()
    {
        var sinceOption = new Option<DateTimeOffset?>(
            "--since",
            "Summarise scans on or after this date (ISO 8601)");

        var cmd = new Command("stats", "Show a summary of recent scans") { sinceOption };

        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var since = ctx.ParseResult.GetValueForOption(sinceOption);
            var entries = await AuditLog.LoadAllAsync(ctx.GetCancellationToken());

            if (since.HasValue)
                entries = [.. entries.Where(e => e.Timestamp >= since.Value)];

            if (entries.Count == 0)
            {
                Console.WriteLine("[audit] No scan records found.");
                return;
            }

            var totalScans = entries.Count;
            var scansWithFindings = entries.Count(e => e.FindingCount > 0);
            var totalFindings = entries.Sum(e => e.FindingCount);
            var topRules = entries
                .SelectMany(e => e.Findings)
                .GroupBy(f => f.RuleId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"  {g.Key}: {g.Count()} finding(s)");

            Console.WriteLine($"[audit] Scans: {totalScans} total, {scansWithFindings} with findings");
            Console.WriteLine($"[audit] Findings: {totalFindings} total");
            Console.WriteLine($"[audit] Log: {AuditLog.LogPath}");
            if (totalFindings > 0)
            {
                Console.WriteLine("[audit] Top rules:");
                foreach (var r in topRules) Console.WriteLine(r);
            }
        });

        return cmd;
    }

    // ── CSV formatter ─────────────────────────────────────────────────────────

    private static string ToCsv(IReadOnlyList<AuditLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ScanId,Timestamp,RepoPath,CommitSha,DiffSource,FilesChanged,FilesEligible,RulesEvaluated,FindingCount,RuleId,RuleName,Confidence,FilePath,Line,Summary");

        foreach (var e in entries)
        {
            if (e.Findings.Count == 0)
            {
                sb.AppendLine(CsvRow(e, null));
            }
            else
            {
                foreach (var f in e.Findings)
                    sb.AppendLine(CsvRow(e, f));
            }
        }
        return sb.ToString();
    }

    private static string CsvRow(AuditLogEntry e, AuditFinding? f) =>
        string.Join(",",
            CsvEscape(e.ScanId),
            CsvEscape(e.Timestamp.ToString("o")),
            CsvEscape(e.RepoPath),
            CsvEscape(e.CommitSha),
            CsvEscape(e.DiffSource),
            e.FilesChanged,
            e.FilesEligible,
            e.RulesEvaluated,
            e.FindingCount,
            CsvEscape(f?.RuleId ?? ""),
            CsvEscape(f?.RuleName ?? ""),
            CsvEscape(f?.Confidence ?? ""),
            CsvEscape(f?.FilePath ?? ""),
            f?.Line?.ToString() ?? "",
            CsvEscape(f?.Summary ?? ""));

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
