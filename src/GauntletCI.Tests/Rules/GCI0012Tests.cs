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
}
