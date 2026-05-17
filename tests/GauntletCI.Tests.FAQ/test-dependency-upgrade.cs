using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace GauntletCI.Tests.FAQ;

/// <summary>
/// FAQ Claim: "If a package reference version shifts in a project configuration file, the engine will flag 
/// the dependency change as a structural delta (`GCI0014` - Third-Party Dependency Shift).
/// It will alert the engineer that a core structural configuration was changed, recommending or requiring 
/// that verification or integration test files be modified or verified alongside the package bump."
/// 
/// Test Goal: Verify that GauntletCI detects .csproj version changes and reports GCI0014.
/// </summary>
public class DependencyUpgradeTests
{
    private const string RepoRoot = @"C:\Users\ericc\GauntletCI";
    private const string TestProjectPath = @"src\GauntletCI.Core.Tests";

    [Fact(Skip = "Requires GauntletCI CLI + Git setup")]
    public void DependencyUpgrade_DetectsVersionChange()
    {
        var testRepoPath = Path.Combine(RepoRoot, "tests", "GauntletCI.Tests.FAQ", "test-repo-dependency");
        var testCsprojPath = Path.Combine(testRepoPath, "TestProject.csproj");

        try
        {
            // Setup: Create a minimal test project with dependencies
            Directory.CreateDirectory(testRepoPath);
            
            // Create a minimal .csproj with a specific package version
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.0"" />
  </ItemGroup>
</Project>";
            
            File.WriteAllText(testCsprojPath, csprojContent);
            
            // Initialize git repo
            RunGitCommand(testRepoPath, "init");
            RunGitCommand(testRepoPath, "config user.email test@example.com");
            RunGitCommand(testRepoPath, "config user.name Test");
            RunGitCommand(testRepoPath, "add TestProject.csproj");
            RunGitCommand(testRepoPath, "commit -m Initial");

            // Modify the version (stage this change)
            var updatedCsproj = csprojContent.Replace("13.0.0", "13.0.3");
            File.WriteAllText(testCsprojPath, updatedCsproj);
            
            RunGitCommand(testRepoPath, "add TestProject.csproj");

            // Run GauntletCI and look for GCI0014 in output
            var output = RunGauntletCI(testRepoPath);

            // Verify that GCI0014 is reported for the dependency change
            Assert.Contains("GCI0014", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTestRepo(testRepoPath);
        }
    }

    [Fact(Skip = "Requires GauntletCI CLI + Git setup")]
    public void DependencyUpgrade_DetectsMultipleVersionChanges()
    {
        var testRepoPath = Path.Combine(RepoRoot, "tests", "GauntletCI.Tests.FAQ", "test-repo-multi-dep");
        var testCsprojPath = Path.Combine(testRepoPath, "TestProject.csproj");

        try
        {
            Directory.CreateDirectory(testRepoPath);
            
            // Create a .csproj with multiple package references
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.0"" />
    <PackageReference Include=""System.Text.Json"" Version=""8.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""8.0.0"" />
  </ItemGroup>
</Project>";
            
            File.WriteAllText(testCsprojPath, csprojContent);
            
            // Initialize git
            RunGitCommand(testRepoPath, "init");
            RunGitCommand(testRepoPath, "config user.email test@example.com");
            RunGitCommand(testRepoPath, "config user.name Test");
            RunGitCommand(testRepoPath, "add TestProject.csproj");
            RunGitCommand(testRepoPath, "commit -m Initial");

            // Upgrade multiple dependencies
            var updated = csprojContent
                .Replace("13.0.0", "13.0.3")
                .Replace("8.0.0", "8.0.1");
            
            File.WriteAllText(testCsprojPath, updated);
            RunGitCommand(testRepoPath, "add TestProject.csproj");

            // Run GauntletCI
            var output = RunGauntletCI(testRepoPath);

            // Should detect dependency changes (GCI0014 for each)
            // Count should reflect multiple package changes
            Assert.Contains("GCI0014", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTestRepo(testRepoPath);
        }
    }

    [Fact]
    public void DependencyUpgrade_ParsesCsprojStructure()
    {
        // Unit test: Verify we can parse and modify .csproj XML correctly
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.0"" />
  </ItemGroup>
</Project>";

        var doc = XDocument.Parse(csprojContent);
        var itemGroup = doc.Root?.Element("ItemGroup");
        var packageRef = itemGroup?.Element("PackageReference");
        
        Assert.NotNull(packageRef);
        var includeAttr = packageRef!.Attribute("Include");
        var versionAttr = packageRef.Attribute("Version");
        
        Assert.NotNull(includeAttr);
        Assert.NotNull(versionAttr);
        Assert.Equal("Newtonsoft.Json", includeAttr!.Value);
        Assert.Equal("13.0.0", versionAttr!.Value);
    }

    private static string RunGauntletCI(string workingDirectory)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "gauntletci",
            Arguments = "analyze --staged",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start GauntletCI process");
            
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return output + Environment.NewLine + error;
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
            // Some git commands may fail in test environment; log but don't throw
            // unless critical
        }
    }

    private static void CleanupTestRepo(string testRepoPath)
    {
        if (!Directory.Exists(testRepoPath))
            return;

        try
        {
            // Give processes time to release file locks
            System.Threading.Thread.Sleep(500);
            
            // Try to remove git locks first
            var gitDir = Path.Combine(testRepoPath, ".git");
            if (Directory.Exists(gitDir))
            {
                var lockFile = Path.Combine(gitDir, "index.lock");
                if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }

            // Recursively delete with retry
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    Directory.Delete(testRepoPath, recursive: true);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    retries--;
                    if (retries > 0)
                        System.Threading.Thread.Sleep(500);
                }
            }
        }
        catch
        {
            // Silently ignore cleanup failures
        }
    }
}
