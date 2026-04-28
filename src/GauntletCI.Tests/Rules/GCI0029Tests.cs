// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0029Tests
{
    private static readonly GCI0029_PiiLoggingLeak Rule = new();

    [Fact]
    public async Task EmailInLoggerCall_ShouldFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    _logger.LogInformation("User email: {Email}", user.email);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("email"));
    }

    [Fact]
    public async Task SsnInLogCall_ShouldFlag()
    {
        var raw = """
            diff --git a/src/AccountService.cs b/src/AccountService.cs
            index abc..def 100644
            --- a/src/AccountService.cs
            +++ b/src/AccountService.cs
            @@ -1,3 +1,4 @@
             public class AccountService {
            +    Log.Information("Account ssn: {Ssn}", user.ssn);
            +    Log.Error("Credit card: {Card}", user.creditcard);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("ssn"));
        Assert.Contains(findings, f => f.Summary.Contains("creditcard"));
    }

    [Fact]
    public async Task LogWithoutPiiTerm_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    _logger.LogInformation("User {UserId} logged in", userId);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PiiTermWithoutLogPrefix_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    var email = user.GetEmail();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NonCsFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/readme.md b/src/readme.md
            index abc..def 100644
            --- a/src/readme.md
            +++ b/src/readme.md
            @@ -1,2 +1,3 @@
             # Docs
            +    _logger.LogInformation("email: {Email}", user.email);
             end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NameInProse_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    _logger.LogInformation("Getting user by name");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("name"));
    }

    [Fact]
    public async Task UsernameInLogCall_ShouldFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    _logger.LogInformation("User {username}", user.userName);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("username"));
    }

    [Fact]
    public async Task ComponentNameInLogCall_ShouldNotFlag()
    {
        // Regression: appender.Name / repository.Name used to cause FPs via the "name" weak term.
        // "name" alone is no longer a PII term; only compound terms like "username" fire.
        var raw = """
            diff --git a/src/AppenderImpl.cs b/src/AppenderImpl.cs
            index abc..def 100644
            --- a/src/AppenderImpl.cs
            +++ b/src/AppenderImpl.cs
            @@ -1,3 +1,4 @@
             public class AppenderImpl {
            +    LogLog.Error(_declaringType, $"Failed to append to appender [{appender.Name}]", e);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task TypeFullNameInLogCall_ShouldNotFlag()
    {
        // Regression: Type.FullName / Assembly.FullName / FileInfo.FullName caused FPs.
        // "fullname" removed from PiiTerms - Type.FullName is a .NET reflection property, not a person name.
        var raw = """
            diff --git a/src/Configurator.cs b/src/Configurator.cs
            index abc..def 100644
            --- a/src/Configurator.cs
            +++ b/src/Configurator.cs
            @@ -1,3 +1,4 @@
             public class Configurator {
            +    LogLog.Error(_declaringType, $"Cannot create [{converterType.FullName}]", e);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task CancellationTokenInLog_ShouldNotFlag()
    {
        // "token" removed from PiiTerms -- CancellationToken was causing FPs
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,3 +1,4 @@
             public class PaymentService {
            +    _logger.LogInformation("Processing payment with token {Token}", cancellationToken);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NetworkAddressInLog_ShouldNotFlag()
    {
        // "address" removed from PiiTerms -- network addresses and method parameters were causing FPs
        var raw = """
            diff --git a/src/ConnectionService.cs b/src/ConnectionService.cs
            index abc..def 100644
            --- a/src/ConnectionService.cs
            +++ b/src/ConnectionService.cs
            @@ -1,3 +1,4 @@
             public class ConnectionService {
            +    _logger.LogInformation("Connecting to address {Address}", serverAddress);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
