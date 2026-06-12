// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Delivery;

namespace GauntletCI.Tests.Rules;

public sealed class RepoDomainClassifierTests
{
    [Fact]
    public void Classify_ConfigOverrideLibrary_ReturnsClassLibrary()
    {
        var diff = new DiffContext();
        var profile = RepoDomainClassifier.Classify(null, diff, new RepoDomainConfig { Profile = "library" });

        Assert.Equal(RepoDomainKind.ClassLibrary, profile.Kind);
    }

    [Fact]
    public void Classify_WebDiffMarkers_ReturnsWebApplication()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Api.cs b/src/Api.cs
            index abc..def 100644
            --- a/src/Api.cs
            +++ b/src/Api.cs
            @@ -1,2 +1,4 @@
             public class Api {
            +    [HttpPost]
            +    public IActionResult Create() => Ok();
             }
            """);

        var profile = RepoDomainClassifier.Classify(null, diff, new RepoDomainConfig());

        Assert.Equal(RepoDomainKind.WebApplication, profile.Kind);
    }

    [Fact]
    public void Classify_LibraryCsprojWithoutWebMarkers_ReturnsClassLibrary()
    {
        var repo = Path.Combine(Path.GetTempPath(), "gci-domain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        try
        {
            File.WriteAllText(Path.Combine(repo, "Client.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var diff = DiffParser.Parse("""
                diff --git a/src/Connection.cs b/src/Connection.cs
                index abc..def 100644
                --- a/src/Connection.cs
                +++ b/src/Connection.cs
                @@ -1,2 +1,3 @@
                 public class Connection {
                +    private readonly Socket _socket = new Socket();
                 }
                """);

            var profile = RepoDomainClassifier.Classify(repo, diff, new RepoDomainConfig());

            Assert.Equal(RepoDomainKind.ClassLibrary, profile.Kind);
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }
}

public sealed class DomainFindingProcessorTests
{
    private static Finding MakeFinding(string ruleId) => new()
    {
        RuleId = ruleId,
        RuleName = ruleId,
        Summary = ruleId,
        Evidence = "evidence",
        WhyItMatters = "why",
        SuggestedAction = "fix",
    };

    [Fact]
    public void Apply_ClassLibraryProfile_DropsConfiguredRules()
    {
        var findings = new[]
        {
            MakeFinding("GCI0038"),
            MakeFinding("GCI0007"),
        };

        var result = DomainFindingProcessor.Apply(
            findings,
            new RepoDomainProfile { Kind = RepoDomainKind.ClassLibrary },
            new RepoDomainConfig());

        Assert.Single(result.Findings);
        Assert.Equal("GCI0007", result.Findings[0].RuleId);
        Assert.Equal(1, result.DroppedCount);
    }
}
