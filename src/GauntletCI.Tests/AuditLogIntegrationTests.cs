// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.Audit;

namespace GauntletCI.Tests;

[CollectionDefinition("AuditLogSerial", DisableParallelization = true)]
public class AuditLogSerialCollection { }

/// <summary>
/// Integration tests for AuditLog that exercise real file I/O against AuditLog.LogPath.
/// Each test backs up the real log file in its constructor and restores it in Dispose
/// so the user's actual audit history is never permanently affected.
/// </summary>
[Collection("AuditLogSerial")]
public class AuditLogIntegrationTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _backupPath;

    public AuditLogIntegrationTests()
    {
        _backupPath = AuditLog.LogPath + ".testbak";
        Directory.CreateDirectory(Path.GetDirectoryName(AuditLog.LogPath)!);

        // Move the real log out of the way so tests start with a clean slate
        if (File.Exists(AuditLog.LogPath))
            File.Move(AuditLog.LogPath, _backupPath, overwrite: true);
    }

    public void Dispose()
    {
        // Remove any file left by the test
        if (File.Exists(AuditLog.LogPath))
            File.Delete(AuditLog.LogPath);

        // Restore the original log
        if (File.Exists(_backupPath))
            File.Move(_backupPath, AuditLog.LogPath, overwrite: true);
    }

    [Fact]
    public async Task AppendAsync_CreatesFileIfNotExists()
    {
        Assert.False(File.Exists(AuditLog.LogPath));

        await AuditLog.AppendAsync(new AuditLogEntry { ScanId = "test-create" });

        Assert.True(File.Exists(AuditLog.LogPath));
    }

    [Fact]
    public async Task AppendAsync_AppendsToExistingFile()
    {
        await AuditLog.AppendAsync(new AuditLogEntry { ScanId = "scan-1" });
        await AuditLog.AppendAsync(new AuditLogEntry { ScanId = "scan-2" });

        var lines = await File.ReadAllLinesAsync(AuditLog.LogPath);
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.Equal(2, nonEmpty.Count);
    }

    [Fact]
    public async Task AppendAsync_WritesValidNdjson()
    {
        await AuditLog.AppendAsync(new AuditLogEntry { ScanId = "ndjson-test", RepoPath = "/test/repo" });

        var lines = await File.ReadAllLinesAsync(AuditLog.LogPath);
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.Single(nonEmpty);
        using var doc = JsonDocument.Parse(nonEmpty[0]);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.TryGetProperty("scanId", out _));
    }

    [Fact]
    public async Task AppendAsync_ConcurrentWrites_AllPersisted()
    {
        var tasks = Enumerable.Range(1, 5)
            .Select(i => AuditLog.AppendAsync(new AuditLogEntry { ScanId = $"concurrent-{i}" }))
            .ToList();

        await Task.WhenAll(tasks);

        var entries = await AuditLog.LoadAllAsync();
        Assert.Equal(5, entries.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Contains(entries, e => e.ScanId == $"concurrent-{i}");
    }

    [Fact]
    public async Task LoadAllAsync_SkipsMalformedLines()
    {
        var e1 = new AuditLogEntry { ScanId = "valid-1" };
        var e2 = new AuditLogEntry { ScanId = "valid-2" };
        var e3 = new AuditLogEntry { ScanId = "valid-3" };

        var content = string.Join(Environment.NewLine,
            JsonSerializer.Serialize(e1, JsonOpts),
            JsonSerializer.Serialize(e2, JsonOpts),
            "{{{INVALID JSON LINE",
            JsonSerializer.Serialize(e3, JsonOpts),
            "");

        await File.WriteAllTextAsync(AuditLog.LogPath, content);

        var entries = await AuditLog.LoadAllAsync();

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.ScanId == "valid-1");
        Assert.Contains(entries, e => e.ScanId == "valid-2");
        Assert.Contains(entries, e => e.ScanId == "valid-3");
    }
}
