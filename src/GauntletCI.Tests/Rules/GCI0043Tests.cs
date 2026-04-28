// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0043Tests
{
    private static readonly GCI0043_NullabilityTypeSafety Rule = new();

    [Fact]
    public async Task EmptyDiff_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    public string GetUser(int id) => _repo.Get(id)?.Name ?? "unknown";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NullForgivingOperator_MoreThanOnce_ShouldFire()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,7 @@
             public class UserService {
            +    public string GetName(User user) {
            +        var name = user.Profile!.DisplayName;
            +        var email = user.Contact!.Email;
            +        return name + email;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Null-forgiving") && f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task NullForgivingOperator_OnlyOnce_ShouldNotFireNullForgivingRule()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,5 @@
             public class UserService {
            +    public string GetName(User user) {
            +        return user.Profile!.DisplayName;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Null-forgiving"));
    }

    [Fact]
    public async Task PragmaWarningDisableNullable_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,5 @@
             public class OrderService {
            +    #pragma warning disable nullable
            +    public string? Name { get; set; }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("pragma warning disable") && f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task PragmaWarningDisableCS8602_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,4 @@
             public class OrderService {
            +    #pragma warning disable CS8602
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("pragma warning disable"));
    }

    [Fact]
    public async Task AsCastWithoutNullCheck_ShouldFire()
    {
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,3 +1,5 @@
             public class PaymentService {
            +    public void Process(object obj) {
            +        var svc = obj as IPaymentGateway;
            +        svc.Charge(100);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("as-cast"));
    }

    [Fact]
    public async Task AsCastWithNullCheck_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,3 +1,6 @@
             public class PaymentService {
            +    public void Process(object obj) {
            +        var svc = obj as IPaymentGateway;
            +        if (svc == null) return;
            +        svc.Charge(100);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("as-cast"));
    }

    [Fact]
    public async Task NullForgivingInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/UserServiceTests.cs b/src/UserServiceTests.cs
            index abc..def 100644
            --- a/src/UserServiceTests.cs
            +++ b/src/UserServiceTests.cs
            @@ -1,3 +1,6 @@
             public class UserServiceTests {
            +    var a = obj!.Name;
            +    var b = other!.Value;
            +    var c = third!.Id;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Null-forgiving"));
    }

    [Fact]
    public async Task XmlDocCommentWithAs_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,5 @@
             public class OrderService {
            +    /// <summary>Returns the result reported as a string.</summary>
            +    public string GetReported() => "ok";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("as-cast"));
    }

    [Fact]
    public async Task RegularCommentWithAs_ShouldNotFire()
    {
        // "as" in a // comment should be ignored
        var raw = """
            diff --git a/src/AnalyzeCommand.cs b/src/AnalyzeCommand.cs
            index abc..def 100644
            --- a/src/AnalyzeCommand.cs
            +++ b/src/AnalyzeCommand.cs
            @@ -1,3 +1,5 @@
             public class AnalyzeCommand {
            +    // Skip posting to GitHub API when --pr-comment-suggest is active (it acts as a dry-run)
            +    public void Run() { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("as-cast"));
    }

    [Fact]
    public async Task AsInStringLiteral_ShouldNotFire()
    {
        // "as" inside a string argument: not a C# as-cast operator
        var raw = """
            diff --git a/src/BaselineCommand.cs b/src/BaselineCommand.cs
            index abc..def 100644
            --- a/src/BaselineCommand.cs
            +++ b/src/BaselineCommand.cs
            @@ -1,3 +1,5 @@
             public class BaselineCommand {
            +    var cmd = new Command("create",
            +        "Run analysis and record all findings as the new baseline");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("as-cast"));
    }

    [Fact]
    public async Task GetValueForOptionNullForgiving_ShouldNotFire()
    {
        // GetValueForOption(opt)! is System.CommandLine's idiomatic pattern for
        // required options: the value is always set and the ! is safe, so
        // multiple occurrences should not trigger the null-forgiving finding.
        var raw = """
            diff --git a/src/GauntletCI.Cli/Commands/AnalyzeCommand.cs b/src/GauntletCI.Cli/Commands/AnalyzeCommand.cs
            index abc..def 100644
            --- a/src/GauntletCI.Cli/Commands/AnalyzeCommand.cs
            +++ b/src/GauntletCI.Cli/Commands/AnalyzeCommand.cs
            @@ -1,3 +1,9 @@
             public class AnalyzeCommand {
            +    private static void Handle(InvocationContext ctx) {
            +        var repo    = ctx.ParseResult.GetValueForOption(repoOption)!;
            +        var output  = ctx.ParseResult.GetValueForOption(outputOption)!;
            +        var severity = ctx.ParseResult.GetValueForOption(severityOption)!;
            +        Process(repo, output, severity);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Null-forgiving"));
    }

    [Fact]
    public async Task AsCastWithValueAccess_GCI0006OwnsIt_ShouldNotFlag()
    {
        // GCI0006 (Edge Case Handling) owns .Value access detection.
        // When an as-cast result is accessed via .Value on the same line, GCI0043 suppresses
        // its as-cast finding to avoid double-reporting the same null-safety defect.
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,2 +1,3 @@
             public class Parser {
            +    var x = (obj as MyType).Value;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("as-cast"));
    }
}