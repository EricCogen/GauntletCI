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
    public async Task EmptyCatch_TaskCanceledException_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (TaskCanceledException)
            +{
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    [Fact]
    public async Task EmptyCatch_OperationCanceledException_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (OperationCanceledException)
            +{
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
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

    [Fact]
    public async Task CatchWithContextLineLog_ShouldNotFlag()
    {
        // The log call is a context line (pre-existing code), not a newly added line.
        // Body scan must include context lines to correctly suppress this finding.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,5 @@
             // service
            +catch (Exception ex)
            +{
             _logger.LogError(ex, "Error occurred");
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    [Fact]
    public async Task CatchWithRemovedThrowAndEmptyBody_ShouldFlag()
    {
        // A throw was removed from the catch body; the body is now empty.
        // The removed throw must NOT suppress detection: Removed lines are excluded.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,4 @@
             // service
            +catch (Exception ex)
            +{
            -    throw;
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Swallowed exception") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task SpecificTypedCatchWithBreakOneLiner_ShouldNotFlag()
    {
        // catch (ChannelClosedException) { break; } on one line: explicit typed handling
        var raw = """
            diff --git a/src/Queue.cs b/src/Queue.cs
            index abc..def 100644
            --- a/src/Queue.cs
            +++ b/src/Queue.cs
            @@ -1,1 +1,2 @@
             // existing
            +catch (ChannelClosedException) { break; } // expected
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    [Fact]
    public async Task SpecificTypedCatchMultiLine_ShouldNotFlag()
    {
        // Multi-line typed catch with explicit handling: should not flag
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,5 @@
             // service
            +catch (IOException)
            +{
            +    return false;
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Swallowed exception"));
    }

    [Fact]
    public async Task ExceptionThrowReplacedWithReturn_CVE202131957_ShouldFlag()
    {
        // CVE-2021-31957: throw Http2ConnectionErrorException(...) replaced with return Task.CompletedTask
        var raw = """
            diff --git a/src/Http2Connection.cs b/src/Http2Connection.cs
            index 86b13a09..10524d02 100644
            --- a/src/Http2Connection.cs
            +++ b/src/Http2Connection.cs
            @@ -718,8 +718,13 @@ namespace Microsoft.AspNetCore.Server.Kestrel
                     // Second reset
                     if (stream.RstStreamReceived)
                     {
            -        // Hard abort
            -        throw new Http2ConnectionErrorException("error", Http2ErrorCode.STREAM_CLOSED);
            +        // Check RFC 7540
            +        return Task.CompletedTask;
                     }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Exception throw") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task ExceptionThrowReplacedWithReturn_InNonErrorPath_ShouldNotFlag()
    {
        // Exception throw replacement outside error handling context should not flag
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,5 @@
              public void Process()
              {
            -    throw new InvalidOperationException("Bad state");
            +    return;
              }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Exception throw"));
    }
}

