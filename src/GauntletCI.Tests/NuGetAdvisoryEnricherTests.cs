// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests;

public sealed class NuGetAdvisoryEnricherTests
{
    [Fact]
    public async Task ExtractPackageNames_CsprojAddedLines_ExtractsPackageName()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/MyApp.csproj
            @@ -1,5 +1,6 @@
            +  <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Single(result);
        Assert.Contains("Newtonsoft.Json", result);
    }

    [Fact]
    public async Task ExtractPackageNames_LockFileAddedLines_ExtractsPackageName()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/packages.lock.json
            @@ -1,5 +1,10 @@
            +"Newtonsoft.Json" : {
            +  "type": "Direct",
            +  "requested": "[13.0.1, )",
            +  "resolved": "13.0.1"
            +}
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Contains("Newtonsoft.Json", result);
    }

    [Fact]
    public async Task ExtractPackageNames_Deduplicates_CaseInsensitive()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/MyApp.csproj
            @@ -1,5 +1,8 @@
            +  <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
            +  <PackageReference Include="newtonsoft.json" Version="13.0.2" />
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Single(result);
    }

    [Fact]
    public async Task ExtractPackageNames_NoNuGetFiles_ReturnsEmpty()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/Foo.cs
            @@ -1,3 +1,4 @@
            + public class Foo {}
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractPackageNames_EmptyDiff_ReturnsEmpty()
    {
        var diffPath = await CreateTempDiffAsync("");
        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractPackageNames_MultiplePackages_ExtractsAll()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/MyApp.csproj
            @@ -1,5 +1,8 @@
            +  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
            +  <PackageReference Include="Serilog" Version="3.1.1" />
            +  <PackageReference Include="Dapper" Version="2.1.0" />
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Equal(3, result.Count);
        Assert.Contains("Microsoft.Extensions.Logging", result);
        Assert.Contains("Serilog", result);
        Assert.Contains("Dapper", result);
    }

    [Fact]
    public async Task ExtractPackageNames_OnlyCountsAddedLines_NotRemovedLines()
    {
        var diffPath = await CreateTempDiffAsync("""
            +++ b/src/MyApp.csproj
            @@ -1,5 +1,5 @@
            -  <PackageReference Include="OldPackage" Version="1.0.0" />
            +  <PackageReference Include="NewPackage" Version="2.0.0" />
            """);

        var result = NuGetAdvisoryEnricher.ExtractPackageNames(diffPath);

        Assert.Single(result);
        Assert.Contains("NewPackage", result);
        Assert.DoesNotContain("OldPackage", result);
    }

    private static async Task<string> CreateTempDiffAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nuget_test_{Guid.NewGuid():N}.patch");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
