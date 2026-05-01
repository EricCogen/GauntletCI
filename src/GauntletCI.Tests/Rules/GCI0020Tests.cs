// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0020Tests
{
    private static readonly GCI0020_ResourceExhaustionPatterns Rule = new();

    private static AnalysisContext MakeContext(DiffContext diff) => new() { Diff = diff };

    [Fact]
    public async Task TimeoutRemoved_ShouldFlag()
    {
        var raw = """
            diff --git a/src/HttpHandler.cs b/src/HttpHandler.cs
            index abc..def 100644
            --- a/src/HttpHandler.cs
            +++ b/src/HttpHandler.cs
            @@ -1,5 +1,3 @@
             public class HttpHandler {
            -    var timeout = TimeSpan.FromSeconds(30);
            -    client.Timeout = timeout;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Single(findings, f => f.Summary.Contains("Timeout") && f.Summary.Contains("removed"));
    }

    [Fact]
    public async Task IterationLimitRemoved_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,7 +1,4 @@
             public class Parser {
            -    int maxIterations = 1000;
             while (condition) {
            -        if (++count > maxIterations) break;
                 Process();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Single(findings, f => f.Summary.Contains("Iteration limit"));
    }

    [Fact]
    public async Task ResourceLimitIncreasedSignificantly_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,3 +1,3 @@
             public class Config {
            -    const int MAX_CONNECTIONS = 1000;
            +    const int MAX_CONNECTIONS = 100000;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Single(findings, f => f.Summary.Contains("increased significantly"));
    }

    [Fact]
    public async Task UsingStatementRemoved_ShouldFlag()
    {
        var raw = """
            diff --git a/src/FileHandler.cs b/src/FileHandler.cs
            index abc..def 100644
            --- a/src/FileHandler.cs
            +++ b/src/FileHandler.cs
            @@ -1,5 +1,4 @@
             public class FileHandler {
            -    using (var stream = File.OpenRead(path)) {
            +    var stream = File.OpenRead(path);
                 Process(stream);
            -    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Single(findings, f => f.Summary.Contains("cleanup") && f.Summary.Contains("removed"));
    }

    [Fact]
    public async Task TaskRunAdded_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,3 +1,3 @@
             public class Processor {
            -    Process(item);
            +    Task.Run(() => Process(item));
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var asyncFinding = findings.FirstOrDefault(f => f.Summary.Contains("async task"));
        Assert.NotNull(asyncFinding);
    }

    [Fact]
    public async Task TimeoutReplacedWithValidValue_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/HttpHandler.cs b/src/HttpHandler.cs
            index abc..def 100644
            --- a/src/HttpHandler.cs
            +++ b/src/HttpHandler.cs
            @@ -1,5 +1,5 @@
             public class HttpHandler {
            -    var timeout = TimeSpan.FromSeconds(30);
            +    var timeout = TimeSpan.FromSeconds(60);
             client.Timeout = timeout;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Timeout"));
    }

    [Fact]
    public async Task SmallResourceLimitIncrease_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,3 +1,3 @@
             public class Config {
            -    const int MAX_CONNECTIONS = 1000;
            +    const int MAX_CONNECTIONS = 1500;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("increased significantly"));
    }

    [Fact]
    public async Task CleanupAddedInDifferentForm_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/FileHandler.cs b/src/FileHandler.cs
            index abc..def 100644
            --- a/src/FileHandler.cs
            +++ b/src/FileHandler.cs
            @@ -1,5 +1,6 @@
             public class FileHandler {
            -    using (var stream = File.OpenRead(path)) {
            +    var stream = File.OpenRead(path);
            +    try {
                 Process(stream);
            +    } finally { stream.Dispose(); }
            -    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        // Different form of cleanup exists (Dispose)
        Assert.DoesNotContain(findings, f => f.Summary.Contains("cleanup") && f.Summary.Contains("removed"));
    }

    [Fact]
    public async Task ResourceRemovalInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/tests/ParserTests.cs b/tests/ParserTests.cs
            index abc..def 100644
            --- a/tests/ParserTests.cs
            +++ b/tests/ParserTests.cs
            @@ -1,5 +1,3 @@
             public class ParserTests {
            -    int maxIterations = 1000;
            -    if (++count > maxIterations) break;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Empty(findings);
    }
}
