// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0042Tests
{
    private static readonly GCI0042_PackageDependencyChanges Rule = new();

    [Fact]
    public async Task NewPackageReference_InCsproj_ShouldFlagLow()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -4,5 +4,6 @@
               <ItemGroup>
            +    <PackageReference Include="SomeLibrary" Version="1.0.0" />
               </ItemGroup>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("New NuGet package reference added") &&
            f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task PackageReference_InNonCsprojFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    // <PackageReference Include="SomeLibrary" Version="1.0.0" />
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task TyposquattedPackageName_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -4,5 +4,6 @@
               <ItemGroup>
            +    <PackageReference Include="Microsft.Extensions.Logging" Version="6.0.0" />
               </ItemGroup>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Suspicious package name") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task LegitimatePackageName_ShouldNotFlagSuspicious()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -4,5 +4,6 @@
               <ItemGroup>
            +    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
               </ItemGroup>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task VersionDowngrade_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -4,6 +4,6 @@
               <ItemGroup>
            -    <PackageReference Include="Newtonsoft.Json" Version="2.0.0" />
            +    <PackageReference Include="Newtonsoft.Json" Version="1.5.0" />
               </ItemGroup>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("version downgraded") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task VersionUpgrade_ShouldNotFlagDowngrade()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -4,6 +4,6 @@
               <ItemGroup>
            -    <PackageReference Include="SomeLib" Version="1.0.0" />
            +    <PackageReference Include="SomeLib" Version="2.0.0" />
               </ItemGroup>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("version downgraded") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task CleanCsproj_ShouldProduceNoFindings()
    {
        var raw = """
            diff --git a/MyApp.csproj b/MyApp.csproj
            index abc..def 100644
            --- a/MyApp.csproj
            +++ b/MyApp.csproj
            @@ -1,4 +1,5 @@
             <Project Sdk="Microsoft.NET.Sdk">
            +  <PropertyGroup>
            +    <TargetFramework>net8.0</TargetFramework>
            +  </PropertyGroup>
             </Project>
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
