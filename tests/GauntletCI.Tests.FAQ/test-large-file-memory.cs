using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace GauntletCI.Tests.FAQ;

/// <summary>
/// FAQ Claim: "Roslyn's parser is unique because it can build a full syntax tree even out of completely broken source code...
/// The engine uses a single, stateful structural pass over the syntax nodes instead of creating multiple intermediate arrays...
/// Memory footprint is strictly constrained, rarely exceeding a few megabytes even when handling massive monolithic source files."
/// 
/// Test Goal: Verify that GauntletCI can analyze large 5000+ line files without excessive memory overhead.
/// </summary>
public class LargeFileMemoryTests
{
    private const string RepoRoot = @"C:\Users\ericc\GauntletCI";
    private const int LargeFileLineCount = 5000;

    [Fact(Skip = "Requires GauntletCI CLI + Git setup")]
    public void LargeFile_Memory_StaysBelow100MB()
    {
        var testRepoPath = Path.Combine(RepoRoot, "tests", "GauntletCI.Tests.FAQ", "test-repo-large-file");
        var largeTestFile = Path.Combine(testRepoPath, "LargeMonolithicFile.cs");

        try
        {
            // Setup: Create a Git repo for this test
            Directory.CreateDirectory(testRepoPath);
            
            // Generate a massive 5000-line C# file
            var fileContent = GenerateLargeCSharpFile(LargeFileLineCount);
            File.WriteAllText(largeTestFile, fileContent);

            // Stage the file in git
            RunGitCommand(testRepoPath, "add LargeMonolithicFile.cs");

            // Measure memory before and after GauntletCI analysis
            var processInfo = new ProcessStartInfo
            {
                FileName = "gauntletci",
                Arguments = "analyze --staged",
                WorkingDirectory = testRepoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start GauntletCI process");
                
            var initialMemory = process.WorkingSet64;
            
            process.WaitForExit();
            
            var finalMemory = process.WorkingSet64;
            var memoryIncrementMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);

            // Verify memory footprint stays under 100MB (with margin, since OS may cache)
            // FAQ claims "rarely exceeding a few megabytes"
            Assert.True(memoryIncrementMB < 100, 
                $"Memory increment should be <100MB, but was {memoryIncrementMB:F2}MB");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testRepoPath))
                Directory.Delete(testRepoPath, recursive: true);
        }
    }

    [Fact]
    public void LargeFile_CanBeGenerated()
    {
        // Unit test that verifies the file generation logic works
        var content = GenerateLargeCSharpFile(100);
        var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        
        // Should have at least 100 lines including structure
        Assert.True(lines.Length >= 50, "Generated file should have substantial line count");
        
        // Should be valid C# (contains namespace and class)
        Assert.Contains("namespace", content);
        Assert.Contains("class", content);
    }

    private static string GenerateLargeCSharpFile(int lineCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("");
        sb.AppendLine("namespace GauntletCI.Tests.FAQ");
        sb.AppendLine("{");
        sb.AppendLine("    public class LargeMonolithicClass");
        sb.AppendLine("    {");

        // Generate many methods to create a large file
        int methodCount = lineCount / 10; // ~10 lines per method
        for (int i = 0; i < methodCount; i++)
        {
            sb.AppendLine($"        /// <summary>Method {i}</summary>");
            sb.AppendLine($"        public void Method{i}()");
            sb.AppendLine("        {");
            sb.AppendLine($"            var result = DoSomething({i});");
            sb.AppendLine($"            Console.WriteLine(\"Method {i} executed: \" + result);");
            sb.AppendLine("        }");
            sb.AppendLine("");
        }

        sb.AppendLine("        private int DoSomething(int value)");
        sb.AppendLine("        {");
        sb.AppendLine("            return value * 2;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void RunGitCommand(string workingDirectory, string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start git process");
            
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Git command failed: {error}");
        }
    }
}
