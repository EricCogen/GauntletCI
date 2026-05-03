// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0010Tests
{
    private static readonly GCI0010_HardcodingAndConfiguration Rule = new(new StubPatternProvider());

    private static DiffContext MakeDiff(string addedLine) =>
        DiffParser.Parse($"""
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,1 +1,2 @@
             // existing
            +{addedLine}
            """);

    [Fact]
    public async Task HardcodedIpAddress_ShouldFlagFinding()
    {
        // Bare IP in a string literal (assignment) should fire.
        var diff = MakeDiff("    var host = \"192.168.1.100\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("IP address"));
    }

    [Fact]
    public async Task HardcodedLocalhostUrl_ShouldFlagFinding()
    {
        // Localhost URL with port is a hardcoded service endpoint.
        var diff = MakeDiff("    var url = \"http://localhost:8080/api\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0010");
    }

    [Fact]
    public async Task PublicHttpsUrl_ShouldNotFlag()
    {
        // Public reference URLs (docs, CDN, GitHub) are intentional: do not flag.
        var diff = MakeDiff("    var docsLink = \"https://docs.microsoft.com/en-us/dotnet/\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("URL"));
    }

    [Fact]
    public async Task HardcodedConnectionString_ShouldFlagFinding()
    {
        var diff = MakeDiff("    var cs = \"Server=myserver;Database=mydb;User Id=sa;Password=pw;\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("connection string"));
    }
}
