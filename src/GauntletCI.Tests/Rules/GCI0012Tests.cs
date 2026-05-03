// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0012Tests
{
    private static readonly GCI0012_SecurityRisk Rule = new(new DefaultPatternProvider());

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
    public async Task SecretVariable_AssignedStringLiteral_ShouldFlag()
    {
        var diff = MakeDiff("    var myToken = \"placeholder-value\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("token"));
    }

    [Fact]
    public async Task TypeNameContainingToken_InComparison_ShouldNotFlag()
    {
        // "token" appears in a type name (HtmlTokenType) in a comparison, not in a variable assignment.
        var diff = MakeDiff("    if (_type == HtmlTokenType.StartTag) return;");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("token"));
    }

    [Fact]
    public async Task TypeNameContainingToken_WithStringLiteralOnRhs_ShouldNotFlag()
    {
        // "token" appears in a type name on the right-hand side; the variable name itself is neutral.
        var diff = MakeDiff("    var prefix = \"Microsoft.IdentityModel.\" + nameof(SecurityTokenInvalidException);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("token"));
    }

    [Fact]
    public async Task EnvVarNameOnly_ShouldNotFlag()
    {
        // String literal looks like an env var name (ALL_CAPS_UNDERSCORES): this is a key reference, not a hardcoded value.
        var diff = MakeDiff("    var apiKey = Environment.GetEnvironmentVariable(\"MY_API_KEY\");");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
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
    public async Task CommentedCredential_DoesNotFlag()
    {
        // GCI0012 skips comment lines for the credential check: commented-out code is
        // historically common (TODOs/examples) and treating it as a hardcoded credential is noisy.
        var diff = MakeDiff("    // var apiKey = \"test1234\";");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }

    [Fact]
    public async Task TokenInLogCall_GCI0029OwnsIt_ShouldNotFlag()
    {
        // GCI0029 (PII Logging Leak) is the authoritative reporter for 'token' in log calls.
        // GCI0012 must not double-report a log call as a hardcoded credential.
        var diff = MakeDiff("    _logger.LogWarning(\"token = \" + authToken);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }

    [Fact]
    public async Task JTokenExpressionBody_ShouldNotFlagCredential()
    {
        // JToken contains "token" but this is an expression-bodied property stub, not a credential assignment.
        var diff = MakeDiff("    public virtual JToken? First => throw new InvalidOperationException(\"Cannot access\");");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }

    [Fact]
    public async Task FormatStringWithTokenInside_ShouldNotFlagCredential()
    {
        // = inside a format string should not be treated as an assignment.
        var diff = MakeDiff("    \"[RedisStreamSequenceToken: EntryId={0}, SeqNum={1}]\",");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }

    [Fact]
    public async Task ActivatorCreateInstanceWithTypeof_ShouldNotFlagDangerous()
    {
        // Controlled type instantiation - typeof() makes the type a compile-time literal.
        var diff = MakeDiff("    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t));");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Dangerous") && f.Summary.Contains("Activator"));
    }

    [Fact]
    public async Task ActivatorCreateInstanceWithCastPattern_ShouldNotFlagDangerous()
    {
        // Cast-pattern Activator.CreateInstance: (KnownType)Activator.CreateInstance(...)
        var diff = MakeDiff("    converter = (ValueConverter)Activator.CreateInstance(converterType);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Dangerous") && f.Summary.Contains("Activator"));
    }

    [Fact]
    public async Task ActivatorCreateInstanceWithVariableType_ShouldFlagDangerous()
    {
        // Variable type with no cast - cannot determine safety statically, should still flag.
        var diff = MakeDiff("    return Activator.CreateInstance(externalType, userInput);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Dangerous"));
    }

    [Fact]
    public async Task TokenFieldAssignedViaMethodCall_ShouldNotFlagCredential()
    {
        // A field named _validateTokenSwitch is a UI element, not a credential.
        // The assigned value is an element ID passed to a factory method, not a bare string literal.
        var diff = MakeDiff("    private readonly IUISwitch _validateTokenSwitch = Switch(\"jwt-decode-validate-token\");");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }

    [Fact]
    public async Task DictionaryKeyWithTokenName_AssignedNonLiteral_ShouldNotFlagCredential()
    {
        // Dictionary key contains "token" but value is a constructor call, not a bare string literal.
        var diff = MakeDiff("    [\"budget_tokens\"] = new(budgetTokens),");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("hardcoded") || f.Summary.Contains("credential"));
    }
}