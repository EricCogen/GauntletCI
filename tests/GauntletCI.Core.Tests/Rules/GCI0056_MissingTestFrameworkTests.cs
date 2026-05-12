// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Core.Tests.Rules;

public class GCI0056_MissingTestFrameworkTests
{
    private GCI0056_MissingTestFramework _rule;

    public GCI0056_MissingTestFrameworkTests()
    {
        _rule = new GCI0056_MissingTestFramework(new DefaultPatternProvider());
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

    [Fact]
    public async Task NoFinding_WhenProjectHasTests()
    {
        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "tests/MyClassTests.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
        };

        var context = CreateContext(files);
        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Finding_WhenNoTestsAndMultipleSources()
    {
        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
        };

        var context = CreateContext(files);
        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
        Assert.Single(findings);
        Assert.Equal(Confidence.Medium, findings[0].Confidence);
    }

    [Fact]
    public async Task NoFinding_WhenTooFewSources()
    {
        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "MyProject.csproj", IsEligible = true }
        };

        var context = CreateContext(files);
        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenNoProjectFile()
    {
        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "src/MyClass.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Service.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "src/Util.cs", IsEligible = true }
        };

        var context = CreateContext(files);
        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenSampleProject()
    {
        var files = new[]
        {
            new ChangedFileAnalysisRecord { FilePath = "samples/Example.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/ExampleService.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/ExampleController.cs", IsEligible = true },
            new ChangedFileAnalysisRecord { FilePath = "samples/Sample.csproj", IsEligible = true }
        };

        var context = CreateContext(files);
        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }
}
