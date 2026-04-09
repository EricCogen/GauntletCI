// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0026Tests
{
    private static readonly GCI0026_DocumentationAdequacy Rule = new();

    [Fact]
    public async Task PublicMethodWithoutXmlDoc_ShouldFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,7 @@
             public class UserService {
            +    public string GetUserName(int id)
            +    {
            +        return _repo.GetName(id);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("GetUserName"));
    }

    [Fact]
    public async Task PublicMethodWithXmlDoc_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,8 @@
             public class UserService {
            +    /// <summary>Gets the user's display name.</summary>
            +    public string GetUserName(int id)
            +    {
            +        return _repo.GetName(id);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PublicMethodWithAttributeButNoDoc_ShouldFlag()
    {
        var raw = """
            diff --git a/src/ApiController.cs b/src/ApiController.cs
            index abc..def 100644
            --- a/src/ApiController.cs
            +++ b/src/ApiController.cs
            @@ -1,3 +1,7 @@
             public class ApiController {
            +    [HttpGet]
            +    public string GetAsync()
            +    {
            +        return "ok";
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("GetAsync"));
    }

    [Fact]
    public async Task PublicMethodInNonCsFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/docs/api.md b/docs/api.md
            index abc..def 100644
            --- a/docs/api.md
            +++ b/docs/api.md
            @@ -1,2 +1,3 @@
             # API Reference
            +public string GetUserName(int id)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
