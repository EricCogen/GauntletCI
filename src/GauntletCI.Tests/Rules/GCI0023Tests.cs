// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0023Tests
{
    private static readonly GCI0023_StructuredLogging Rule = new();

    [Fact]
    public async Task LogCallWithInterpolation_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,3 @@
             public class Service {
            +    _logger.LogInformation($"Processing order {orderId} for user {userId}");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("string interpolation"));
    }

    [Fact]
    public async Task LogCallWithTemplate_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,3 @@
             public class Service {
            +    _logger.LogInformation("Processing order {OrderId} for user {UserId}", orderId, userId);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("string interpolation"));
    }

    [Fact]
    public async Task CriticalPathLoggingWithoutCorrelationId_ShouldFlag()
    {
        // File is in auth path, has logging, but no CorrelationId/RequestId/TraceId
        var addedLines = string.Join("\n",
            Enumerable.Range(1, 6).Select(i => $"+    _logger.LogInformation(\"Step {i}\");"));

        var raw = "diff --git a/src/AuthService.cs b/src/AuthService.cs\n" +
            "index abc..def 100644\n" +
            "--- a/src/AuthService.cs\n" +
            "+++ b/src/AuthService.cs\n" +
            "@@ -1,1 +1,7 @@\n" +
            " public class AuthService {\n" +
            addedLines + "\n" +
            " }";

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("correlation") || f.Summary.Contains("request ID"));
    }

    [Fact]
    public async Task CriticalPathLoggingWithCorrelationId_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,1 +1,8 @@
             public class PaymentService {
            +    var correlationId = context.RequestId;
            +    _logger.LogInformation("Processing {CorrelationId}", correlationId);
            +    _logger.LogInformation("Step 2 {CorrelationId}", correlationId);
            +    _logger.LogInformation("Step 3 {CorrelationId}", correlationId);
            +    _logger.LogInformation("Step 4 {CorrelationId}", correlationId);
            +    _logger.LogInformation("Step 5 {CorrelationId}", correlationId);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("no correlation"));
    }
}
