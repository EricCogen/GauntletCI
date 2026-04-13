// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

/// <summary>
/// GCI0030 is now superseded by GCI0024 (Resource Lifecycle), which absorbs all
/// suffix-based disposable detection. These tests validate the absorbed behavior via
/// GCI0024, and verify GCI0030 itself no longer produces findings.
/// </summary>
public class GCI0030Tests
{
    private static readonly GCI0024_ResourceLifecycle Rule = new();
    private static readonly GCI0030_DisposableResourceSafety SupersededRule = new();

    [Fact]
    public async Task GCI0030_IsSuperseded_ShouldReturnNoFindings()
    {
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,3 +1,4 @@
             public class Parser {
            +    var reader = new StreamReader("file.txt");
             }
            """;
        var diff = DiffParser.Parse(raw);
        var findings = await SupersededRule.EvaluateAsync(diff, null);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task StreamReaderWithoutUsing_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,3 +1,4 @@
             public class Parser {
            +    var reader = new StreamReader("file.txt");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("StreamReader") && f.Summary.Contains("using"));
    }

    [Fact]
    public async Task DbConnectionWithoutUsing_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Repo.cs b/src/Repo.cs
            index abc..def 100644
            --- a/src/Repo.cs
            +++ b/src/Repo.cs
            @@ -1,3 +1,4 @@
             public class Repo {
            +    var conn = new DbConnection(connectionString);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("DbConnection"));
    }

    [Fact]
    public async Task StreamReaderWithUsing_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,3 +1,4 @@
             public class Parser {
            +    using var reader = new StreamReader("file.txt");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("StreamReader"));
    }

    [Fact]
    public async Task HttpClientWithDisposeWindow_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Api.cs b/src/Api.cs
            index abc..def 100644
            --- a/src/Api.cs
            +++ b/src/Api.cs
            @@ -1,6 +1,9 @@
             public class Api {
            +    var client = new HttpClient();
            +    try {
            +        client.GetAsync(url);
            +    } finally {
            +        client.Dispose();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient"));
    }

    [Fact]
    public async Task TypeWithNoDisposableSuffix_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    var dto = new UserDto(name, age);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
