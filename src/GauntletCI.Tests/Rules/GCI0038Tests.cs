// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0038Tests
{
    private static readonly GCI0038_DependencyInjectionSafety Rule = new();

    [Fact]
    public async Task ServiceLocator_InNonInfrastructureFile_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/UserController.cs b/src/UserController.cs
            index abc..def 100644
            --- a/src/UserController.cs
            +++ b/src/UserController.cs
            @@ -1,3 +1,5 @@
             public class UserController {
            +    var svc = _serviceProvider.GetRequiredService<IUserService>();
            +    var foo = serviceProvider.GetService<IFoo>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Service locator") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task ServiceLocator_InStartupCs_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Startup.cs b/src/Startup.cs
            index abc..def 100644
            --- a/src/Startup.cs
            +++ b/src/Startup.cs
            @@ -1,3 +1,4 @@
             public class Startup {
            +    var svc = serviceProvider.GetRequiredService<IUserService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Service locator"));
    }

    [Fact]
    public async Task ServiceLocator_InExtensionsFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/ServiceExtensions.cs b/src/ServiceExtensions.cs
            index abc..def 100644
            --- a/src/ServiceExtensions.cs
            +++ b/src/ServiceExtensions.cs
            @@ -1,3 +1,4 @@
             public static class ServiceExtensions {
            +    var svc = provider.GetRequiredService<IUserService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Service locator"));
    }

    [Fact]
    public async Task DirectInstantiation_InProductionCode_ShouldFlagLow()
    {
        var raw = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index abc..def 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -1,3 +1,4 @@
             public class OrderController {
            +    var svc = new UserService(db);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Direct instantiation") &&
            f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task DirectInstantiation_InTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserServiceTests.cs b/src/UserServiceTests.cs
            index abc..def 100644
            --- a/src/UserServiceTests.cs
            +++ b/src/UserServiceTests.cs
            @@ -1,3 +1,4 @@
             public class UserServiceTests {
            +    var svc = new UserService(mockDb);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Direct instantiation"));
    }

    [Fact]
    public async Task DirectInstantiation_MockType_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index abc..def 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -1,3 +1,4 @@
             public class OrderController {
            +    var mock = new Mock<UserService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Direct instantiation"));
    }

    [Fact]
    public async Task CaptiveDependency_SameFileAddsSingletonAndScoped_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/DependencyRegistration.cs b/src/DependencyRegistration.cs
            index abc..def 100644
            --- a/src/DependencyRegistration.cs
            +++ b/src/DependencyRegistration.cs
            @@ -1,3 +1,6 @@
             public static class DependencyRegistration {
            +    services.AddSingleton<IFooService, FooService>();
            +    services.AddScoped<IBarService, BarService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("captive dependency") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task CaptiveDependency_SameFileAddsSingletonAndTransient_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/DependencyRegistration.cs b/src/DependencyRegistration.cs
            index abc..def 100644
            --- a/src/DependencyRegistration.cs
            +++ b/src/DependencyRegistration.cs
            @@ -1,3 +1,6 @@
             public static class DependencyRegistration {
            +    services.AddSingleton<ICacheService, CacheService>();
            +    services.AddTransient<IEmailService, EmailService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("captive dependency") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task CleanFile_ShouldProduceNoFindings()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    public string GetUser(int id) => _repo.Get(id)?.Name ?? "unknown";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
