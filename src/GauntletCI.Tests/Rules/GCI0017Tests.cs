// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0017Tests
{
    private static readonly GCI0017_ScopeDiscipline Rule = new();

    [Fact]
    public async Task ThreeDistinctTopLevelDirs_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +int x = 1;
            diff --git a/tests/ServiceTests.cs b/tests/ServiceTests.cs
            index abc..def 100644
            --- a/tests/ServiceTests.cs
            +++ b/tests/ServiceTests.cs
            @@ -1,1 +1,2 @@
             // test
            +int y = 2;
            diff --git a/docs/README.md b/docs/README.md
            index abc..def 100644
            --- a/docs/README.md
            +++ b/docs/README.md
            @@ -1,1 +1,2 @@
             # Docs
            +## New Section
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("3 distinct top-level"));
    }

    [Fact]
    public async Task TwoDistinctDirs_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +int x = 1;
            diff --git a/src/Repository.cs b/src/Repository.cs
            index abc..def 100644
            --- a/src/Repository.cs
            +++ b/src/Repository.cs
            @@ -1,1 +1,2 @@
             // repository
            +int y = 2;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("distinct top-level"));
    }

    [Fact]
    public async Task MixedProductionAndMigration_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Migrations/AddUsers.cs b/src/Migrations/AddUsers.cs
            index abc..def 100644
            --- a/src/Migrations/AddUsers.cs
            +++ b/src/Migrations/AddUsers.cs
            @@ -1,1 +1,2 @@
             // migration
            +void Up() { }
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Non-production files"));
    }
}
