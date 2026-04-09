// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0007Tests
{
    private static readonly GCI0007_ErrorHandlingIntegrity Rule = new();

    [Fact]
    public async Task EmptyCatchBlock_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (Exception ex)
            +{
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Swallowed exception") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task CatchWithLog_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (Exception ex)
            +{
            +    _logger.LogError(ex, "Error occurred");
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    [Fact]
    public async Task CatchWithRethrow_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (Exception ex)
            +{
            +    throw;
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }
}
