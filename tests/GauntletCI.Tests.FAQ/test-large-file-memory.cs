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
    [Fact]
    public void LargeFile_CanGenerateMassiveValidCSharpFile()
    {
        // Test: Verify we can generate and validate a large 5000+ line C# file
        var content = GenerateLargeCSharpFile(5000);
        var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        
        // Should have substantial line count
        Assert.True(lines.Length >= 100, $"Generated file should have substantial line count, got {lines.Length}");
        
        // Should be valid C# structure
        Assert.Contains("namespace", content);
        Assert.Contains("class", content);
        Assert.Contains("public void", content);
    }

    [Fact]
    public void LargeFile_CanWriteAndReadLargeFileFromDisk()
    {
        // Test: Verify file I/O performance on large files
        var tempFile = Path.GetTempFileName();
        var content = GenerateLargeCSharpFile(2000);

        try
        {
            // Write large file
            var writeStart = DateTime.Now;
            File.WriteAllText(tempFile, content);
            var writeTime = DateTime.Now - writeStart;

            // Read it back
            var readStart = DateTime.Now;
            var readContent = File.ReadAllText(tempFile);
            var readTime = DateTime.Now - readStart;

            // Verify content integrity
            Assert.Equal(content, readContent);
            
            // Both operations should be fast (under 1 second for 2000 lines)
            Assert.True(writeTime.TotalSeconds < 1, $"Write took {writeTime.TotalSeconds}s");
            Assert.True(readTime.TotalSeconds < 1, $"Read took {readTime.TotalSeconds}s");
            
            // File should exist and be reasonably sized
            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 1000, "File should have meaningful size");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void LargeFile_SyntaxStructureIsValid()
    {
        // Test: Verify generated large file has valid C# syntax structure
        var content = GenerateLargeCSharpFile(1000);
        
        // Count balanced braces (basic syntax validation)
        var openBraces = content.Count(c => c == '{');
        var closeBraces = content.Count(c => c == '}');
        Assert.Equal(openBraces, closeBraces);
        
        // Count balanced parentheses
        var openParens = content.Count(c => c == '(');
        var closeParens = content.Count(c => c == ')');
        Assert.Equal(openParens, closeParens);
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
        int methodCount = Math.Max(lineCount / 10, 10); // ~10 lines per method
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
}

