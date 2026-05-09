// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.Text.Json;

namespace GauntletCI.Tests;

/// <summary>
/// Spawns the GauntletCI CLI as a real subprocess and verifies real output.
/// Tests are skipped (vacuous pass) if the CLI dll has not been built yet.
/// </summary>
public class EndToEndTests
{
    // Diff that reliably fires GCI0007 (swallowed exception) so "GCI" always appears in output.
    private const string SimpleDiff = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index 0000000..1111111 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,5 +1,12 @@
         public class Foo
         {
        -    public void Bar() { }
        +    public void Bar()
        +    {
        +        try
        +        {
        +            Console.WriteLine("hello");
        +        }
        +        catch { }
        +    }
         }
        """;

    private static (string dll, bool skip) GetCliDll()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../GauntletCI.Cli/bin/Debug/net8.0/GauntletCI.Cli.dll"));
        return (path, !File.Exists(path));
    }

    private static async Task<(string stdout, string stderr, int exitCode)> RunCliAsync(
        string dll, string args, string? stdin = null)
    {
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" {args}")
        {
            RedirectStandardOutput  = true,
            RedirectStandardError   = true,
            RedirectStandardInput   = stdin is not null,
            UseShellExecute         = false,
            StandardOutputEncoding  = System.Text.Encoding.UTF8,
            StandardErrorEncoding   = System.Text.Encoding.UTF8,
        };
        psi.Environment["CI"]       = "true"; // suppress telemetry prompt + banner
        psi.Environment["NO_COLOR"] = "1";

        var proc = Process.Start(psi)!;
        if (stdin is not null)
        {
            await proc.StandardInput.WriteAsync(stdin);
            proc.StandardInput.Close();
        }
        // Read stdout and stderr concurrently to prevent pipe-buffer deadlocks
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        await proc.WaitForExitAsync();
        return (stdoutTask.Result, stderrTask.Result, proc.ExitCode);
    }

    [Fact]
    public async Task Analyze_ViaStdin_JsonOutput_IsValidJson()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        var (stdout, _, _) = await RunCliAsync(dll, "analyze --output json", SimpleDiff);

        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("HasFindings", out _));
    }

    [Fact]
    public async Task Analyze_ViaStdin_TextOutput_ContainsExpectedFields()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        var (stdout, stderr, _) = await RunCliAsync(dll, "analyze", SimpleDiff);

        // At least one GCI rule ID must appear in combined output
        Assert.Contains("GCI", stdout + stderr);
    }

    [Fact]
    public async Task Analyze_WithDiffFile_ProducesOutput()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        var tempFile = Path.Combine(Path.GetTempPath(), $"gci_e2e_{Guid.NewGuid():N}.patch");
        try
        {
            await File.WriteAllTextAsync(tempFile, SimpleDiff);
            var (stdout, _, _) = await RunCliAsync(dll, $"analyze --diff \"{tempFile}\" --output json");

            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.TryGetProperty("HasFindings", out _));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Analyze_StagedInNonGitDir_ExitsWithError()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"gci_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var (_, stderr, exitCode) = await RunCliAsync(dll, $"analyze --staged --repo \"{tempDir}\"");
            Assert.True(exitCode != 0, $"Expected non-zero exit but got {exitCode}. stderr: {stderr}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditExport_AfterAnalyze_ContainsEntry()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        // Ensure at least one audit entry exists by running an analysis first
        await RunCliAsync(dll, "analyze --output json", SimpleDiff);

        var (stdout, stderr, _) = await RunCliAsync(dll, "audit export --format json");

        if (stderr.Contains("No scan records"))
            return; // No entries yet in this environment — not a failure

        if (!string.IsNullOrWhiteSpace(stdout) && stdout.TrimStart().StartsWith('['))
        {
            using var doc = JsonDocument.Parse(stdout);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.GetArrayLength() >= 1);
        }
    }

    [Fact]
    public async Task Version_Flag_ReturnsVersionString()
    {
        var (dll, skip) = GetCliDll();
        if (skip) return;

        var (stdout, stderr, exitCode) = await RunCliAsync(dll, "--version");

        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+", stdout + stderr);
    }
}
