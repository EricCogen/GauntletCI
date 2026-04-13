// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0020Tests
{
    private static readonly GCI0020_AccountabilityStandard Rule = new();

    [Fact]
    public async Task CatchException_ShouldNotFlag_OwnerIsGCI0007()
    {
        // Swallowed catch(Exception) detection is owned by GCI0007 (Error Handling Integrity).
        // GCI0020 does not duplicate this check.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +catch (Exception ex) { }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("catch (Exception)"));
    }

    [Fact]
    public async Task PasswordAssignment_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,1 +1,2 @@
             // config
            +var password = "abc123";
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("credential pattern") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task FiveConsecutiveComments_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,7 @@
             // service
            +// line one
            +// line two
            +// line three
            +// line four
            +// line five
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("consecutive comment lines"));
    }

    [Fact]
    public async Task FourConsecutiveComments_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,6 @@
             // service
            +// line one
            +// line two
            +// line three
            +// line four
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("consecutive comment lines"));
    }

    [Fact]
    public async Task EmptyRolesAttribute_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Controller.cs b/src/Controller.cs
            index abc..def 100644
            --- a/src/Controller.cs
            +++ b/src/Controller.cs
            @@ -1,1 +1,2 @@
             // controller
            +[Authorize(Roles = "")]
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Empty Roles") || f.Summary.Contains("empty Roles"));
    }

    [Fact]
    public async Task UnreachableCodeAfterReturn_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,4 @@
             // service
            +return result;
            +DoSomethingElse();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("unreachable code"));
    }

    [Fact]
    public async Task ReturnFollowedByClosingBrace_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,4 @@
             // service
            +return result;
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("unreachable code"));
    }

    [Fact]
    public async Task SecretApiKey_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,1 +1,2 @@
             // config
            +var apikey = "sk-12345abcdef";
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("credential pattern") &&
            f.Confidence == Confidence.High);
    }
}
