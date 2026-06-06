// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Core.Tests.Rules;

public class GCI0056_MissingTestFrameworkTests : IDisposable
{
    private readonly GCI0056_MissingTestFramework _rule;
    private readonly string _originalDirectory;

    public GCI0056_MissingTestFrameworkTests()
    {
        _rule = new GCI0056_MissingTestFramework(new DefaultPatternProvider());
        _originalDirectory = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
    }

    private AnalysisContext CreateContext(params ChangedFileAnalysisRecord[] files)
    {
        var diffContext = new DiffContext { CommitSha = "test", Files = [] };
        return new AnalysisContext
        {
            EligibleFiles = files.ToList(),
            SkippedFiles = [],
            Diff = diffContext
        };
    }

    private static string CreateTempRepo(Action<string> setup)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gci0056-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        setup(tempDir);
        Directory.SetCurrentDirectory(tempDir);
        return tempDir;
    }

    [Fact]
    public async Task NoFinding_WhenProjectHasTests()
    {
        var tempDir = CreateTempRepo(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            Directory.CreateDirectory(Path.Combine(dir, "tests"));
            File.WriteAllText(Path.Combine(dir, "src", "MyClass.cs"), "class MyClass {}");
            File.WriteAllText(Path.Combine(dir, "src", "Service.cs"), "class Service {}");
            File.WriteAllText(Path.Combine(dir, "src", "Util.cs"), "class Util {}");
            File.WriteAllText(Path.Combine(dir, "tests", "MyClassTests.cs"), "class MyClassTests {}");
            File.WriteAllText(Path.Combine(dir, "MyProject.csproj"), "<Project />");
        });

        try
        {
            var files = new[]
            {
                new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "tests/MyClassTests.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
            };

            var findings = await _rule.EvaluateAsync(CreateContext(files));
            Assert.Empty(findings);
        }
        finally
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NoFinding_WhenProjectReferencesTestFramework()
    {
        var tempDir = CreateTempRepo(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "MyClass.cs"), "class MyClass {}");
            File.WriteAllText(Path.Combine(dir, "src", "Service.cs"), "class Service {}");
            File.WriteAllText(Path.Combine(dir, "src", "Util.cs"), "class Util {}");
            File.WriteAllText(
                Path.Combine(dir, "MyProject.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="xunit" Version="2.9.3" />
                  </ItemGroup>
                </Project>
                """);
        });

        try
        {
            var files = new[]
            {
                new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
            };

            var findings = await _rule.EvaluateAsync(CreateContext(files));
            Assert.Empty(findings);
        }
        finally
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Finding_WhenNoTestsAndMultipleSources()
    {
        var tempDir = CreateTempRepo(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "MyClass.cs"), "class MyClass {}");
            File.WriteAllText(Path.Combine(dir, "src", "Service.cs"), "class Service {}");
            File.WriteAllText(Path.Combine(dir, "src", "Util.cs"), "class Util {}");
            File.WriteAllText(Path.Combine(dir, "MyProject.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        });

        try
        {
            var files = new[]
            {
                new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
            };

            var findings = await _rule.EvaluateAsync(CreateContext(files));

            Assert.NotEmpty(findings);
            Assert.Single(findings);
            Assert.Equal(Confidence.Medium, findings[0].Confidence);
        }
        finally
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NoFinding_WhenTooFewSources()
    {
        var tempDir = CreateTempRepo(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "MyClass.cs"), "class MyClass {}");
            File.WriteAllText(Path.Combine(dir, "src", "Service.cs"), "class Service {}");
            File.WriteAllText(Path.Combine(dir, "MyProject.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        });

        try
        {
            var files = new[]
            {
                new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
                new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
            };

            var findings = await _rule.EvaluateAsync(CreateContext(files));
            Assert.Empty(findings);
        }
        finally
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NoFinding_WhenNoProjectFile()
    {
        CreateTempRepo(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "MyClass.cs"), "class MyClass {}");
            File.WriteAllText(Path.Combine(dir, "src", "Service.cs"), "class Service {}");
            File.WriteAllText(Path.Combine(dir, "src", "Util.cs"), "class Util {}");
        });

        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true }
        };

        var findings = await _rule.EvaluateAsync(CreateContext(files));
        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenSampleProject()
    {
        CreateTempRepo(_ => { });

        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "samples/Example.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/ExampleService.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/ExampleController.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/Sample.csproj", IsEligible = true }
        };

        var findings = await _rule.EvaluateAsync(CreateContext(files));
        Assert.Empty(findings);
    }
}
