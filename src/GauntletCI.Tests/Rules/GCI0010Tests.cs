// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0010Tests
{
    private static readonly GCI0010_HardcodingAndConfiguration Rule = new();

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
        var diff = MakeDiff("    var host = \"192.168.1.100\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("IP address"));
    }

    [Fact]
    public async Task HardcodedConnectionString_ShouldFlagFinding()
    {
        var diff = MakeDiff("    var cs = \"Server=myserver;Database=mydb;User Id=sa;Password=pw;\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("connection string"));
    }

    [Fact]
    public async Task SecretVariable_AssignedStringLiteral_ShouldFlag()
    {
        var diff = MakeDiff("    var myToken = \"placeholder-value\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("token"));
    }

    [Fact]
    public async Task TypeNameContainingToken_InComparison_ShouldNotFlag()
    {
        // "token" appears in a type name (HtmlTokenType) in a comparison, not in a variable assignment.
        var diff = MakeDiff("    if (_type == HtmlTokenType.StartTag) return;");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("token"));
    }

    [Fact]
    public async Task TypeNameContainingToken_WithStringLiteralOnRhs_ShouldNotFlag()
    {
        // "token" appears in a type name on the right-hand side; the variable name itself is neutral.
        var diff = MakeDiff("    var prefix = \"Microsoft.IdentityModel.\" + nameof(SecurityTokenInvalidException);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("token"));
    }
}
