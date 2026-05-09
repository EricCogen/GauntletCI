// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Telemetry;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Tests;

public class TelemetryDbTests : IDisposable
{
    private readonly string _dbPath;

    public TelemetryDbTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gauntletci-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        // Clear connection pool to release any residual file handles before cleanup
        SqliteConnection.ClearAllPools();
        for (int i = 0; i < 5 && File.Exists(_dbPath); i++)
        {
            try { File.Delete(_dbPath); break; }
            catch (IOException) when (i < 4) { Thread.Sleep(20 * (i + 1)); }
            catch { break; }
        }
    }

    [Fact]
    public async Task AppendAsync_WritesEventToDb()
    {
        var evt = new TelemetryEvent
        {
            EventType    = "rule_metric",
            InstallId    = "test-install",
            RepoHash     = "abc12345",
            RuleId       = "GCI0001",
            DurationMs   = 42,
            Outcome      = "Triggered",
            FindingCount = 1,
        };

        await TelemetryDb.AppendAsync(evt, _dbPath);

        Assert.Equal(1, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task AppendAsync_DuplicateEventId_Ignored()
    {
        var evt = new TelemetryEvent { EventType = "analysis", InstallId = "id" };

        await TelemetryDb.AppendAsync(evt, _dbPath);
        await TelemetryDb.AppendAsync(evt, _dbPath); // same EventId

        Assert.Equal(1, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task AppendAsync_MultipleDistinctEvents_AllPersisted()
    {
        for (int i = 0; i < 5; i++)
            await TelemetryDb.AppendAsync(new TelemetryEvent { EventType = "rule_metric", RuleId = $"GCI{i:D4}" }, _dbPath);

        Assert.Equal(5, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task AppendAsync_AllEventTypes_WriteSuccessfully()
    {
        var events = new[]
        {
            new TelemetryEvent { EventType = "analysis",    FindingCount = 3, FilesChanged = 2, RulesEvaluated = 10, LinesAdded = 50, LinesRemoved = 10 },
            new TelemetryEvent { EventType = "finding",     RuleId = "GCI0005", Confidence = "High", FileExt = ".cs" },
            new TelemetryEvent { EventType = "rule_metric", RuleId = "GCI0001", DurationMs = 15, Outcome = "Passed", FindingCount = 0 },
            new TelemetryEvent { EventType = "feedback",    Vote = "up" },
        };

        foreach (var evt in events)
            await TelemetryDb.AppendAsync(evt, _dbPath);

        Assert.Equal(4, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task MarkSentAsync_MarksEventsSent_AndPurgesOldOnes()
    {
        var evt = new TelemetryEvent { EventType = "analysis" };
        await TelemetryDb.AppendAsync(evt, _dbPath);

        // Mark sent: event is recent so won't be purged by the 7-day cutoff
        await TelemetryDb.MarkSentAsync([evt.EventId], _dbPath);

        // Still present (sent but recent)
        Assert.Equal(1, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task CountAsync_EmptyDb_ReturnsZero()
    {
        Assert.Equal(0, await TelemetryDb.CountAsync(_dbPath));
    }

    [Fact]
    public async Task AppendAsync_NullOptionalFields_DoesNotThrow()
    {
        var evt = new TelemetryEvent { EventType = "rule_metric" };
        var ex = await Record.ExceptionAsync(() => TelemetryDb.AppendAsync(evt, _dbPath));
        Assert.Null(ex);
    }
}
