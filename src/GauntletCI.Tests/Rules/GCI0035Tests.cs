// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0035Tests
{
    private static GCI0035_ArchitectureLayerGuard CreateRule() =>
        CreateRule(new() { ["Domain"] = ["Infrastructure"] });

    private static GCI0035_ArchitectureLayerGuard CreateRule(Dictionary<string, List<string>> imports)
    {
        var rule = new GCI0035_ArchitectureLayerGuard();
        rule.Configure(new GauntletConfig { ForbiddenImports = imports });
        return rule;
    }

    [Fact]
    public async Task DomainImportingInfrastructure_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Domain/UserAggregate.cs b/src/Domain/UserAggregate.cs
            index abc..def 100644
            --- a/src/Domain/UserAggregate.cs
            +++ b/src/Domain/UserAggregate.cs
            @@ -1,3 +1,4 @@
             namespace MyApp.Domain;
            +using MyApp.Infrastructure.Data;
             public class UserAggregate { }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = CreateRule();
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Infrastructure") && f.Summary.Contains("Domain"));
    }

    [Fact]
    public async Task DomainImportingAllowedNamespace_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Domain/UserAggregate.cs b/src/Domain/UserAggregate.cs
            index abc..def 100644
            --- a/src/Domain/UserAggregate.cs
            +++ b/src/Domain/UserAggregate.cs
            @@ -1,3 +1,4 @@
             namespace MyApp.Domain;
            +using System.Collections.Generic;
             public class UserAggregate { }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = CreateRule();
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task InfrastructureImportingInfrastructure_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Infrastructure/Repo.cs b/src/Infrastructure/Repo.cs
            index abc..def 100644
            --- a/src/Infrastructure/Repo.cs
            +++ b/src/Infrastructure/Repo.cs
            @@ -1,3 +1,4 @@
             namespace MyApp.Infrastructure;
            +using MyApp.Infrastructure.Data;
             public class Repo { }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = CreateRule();
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoForbiddenImportsConfig_ShouldReturnInformationalFinding()
    {
        var raw = """
            diff --git a/src/Domain/Foo.cs b/src/Domain/Foo.cs
            index abc..def 100644
            --- a/src/Domain/Foo.cs
            +++ b/src/Domain/Foo.cs
            @@ -1,3 +1,4 @@
             namespace MyApp.Domain;
            +using MyApp.Infrastructure.Data;
             public class Foo { }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = new GCI0035_ArchitectureLayerGuard();
        rule.Configure(new GauntletConfig { ForbiddenImports = null });
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Single(findings);
        Assert.Contains(findings, f => f.Summary.Contains("not configured"));
    }

    [Fact]
    public async Task MultipleLayerRules_ShouldFlagEachViolation()
    {
        var raw = """
            diff --git a/src/Application/Handler.cs b/src/Application/Handler.cs
            index abc..def 100644
            --- a/src/Application/Handler.cs
            +++ b/src/Application/Handler.cs
            @@ -1,3 +1,4 @@
             namespace MyApp.Application;
            +using MyApp.Infrastructure.Persistence;
             public class Handler { }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = CreateRule(new() { ["Application"] = ["Infrastructure"] });
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Infrastructure") && f.Summary.Contains("Application"));
    }
}
