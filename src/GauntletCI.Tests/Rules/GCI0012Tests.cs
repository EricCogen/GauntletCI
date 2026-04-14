// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0012Tests
{
    private static readonly GCI0012_SecurityRisk Rule = new();

    private static DiffContext MakeDiff(string addedLine) =>
        DiffParser.Parse($"""
            diff --git a/src/Repo.cs b/src/Repo.cs
            index abc..def 100644
            --- a/src/Repo.cs
            +++ b/src/Repo.cs
            @@ -1,1 +1,2 @@
             // existing
            +{addedLine}
            """);

    [Fact]
    public async Task SqlStringConcatenation_ShouldFlagSqlInjection()
    {
        var diff = MakeDiff("    var sql = \"SELECT * FROM Users WHERE Name = '\" + userName + \"'\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("SQL injection"));
    }

    [Fact]
    public async Task Md5Create_ShouldFlagWeakHashing()
    {
        var diff = MakeDiff("    using var md5 = MD5.Create();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Weak hashing") || f.Summary.Contains("MD5"));
    }

    [Fact]
    public async Task Sha1Managed_ShouldFlagWeakHashing()
    {
        var diff = MakeDiff("    using var sha1 = new SHA1Managed();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Weak hashing") || f.Summary.Contains("SHA1"));
    }

    [Fact]
    public async Task HardcodedPassword_ShouldFlagCredentialLeak()
    {
        var diff = MakeDiff("    var password = \"mysecret123\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential") || f.Summary.Contains("secret"));
    }

    [Fact]
    public async Task ParameterizedSql_ShouldNotFlag()
    {
        var diff = MakeDiff("    var sql = \"SELECT * FROM Users WHERE Id = @id\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("SQL injection"));
    }

    [Fact]
    public async Task Sha256_ShouldNotFlagWeakHashing()
    {
        var diff = MakeDiff("    using var sha256 = SHA256.Create();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Weak hashing"));
    }

    [Fact]
    public async Task CommentedCredential_StillFlags()
    {
        // Note: GCI0012 doesn't filter comments - this documents actual behavior
        // (credentials in comments are still risky as they can end up in version control)
        var diff = MakeDiff("    // var apiKey = \"test1234\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential") || f.Summary.Contains("apikey"));
    }
}
