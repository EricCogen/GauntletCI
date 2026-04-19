// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0024Tests
{
    private static readonly GCI0024_ResourceLifecycle Rule = new();

    [Fact]
    public async Task FileStreamWithoutUsing_ShouldFlag()
    {
        var raw = """
            diff --git a/src/FileProcessor.cs b/src/FileProcessor.cs
            index abc..def 100644
            --- a/src/FileProcessor.cs
            +++ b/src/FileProcessor.cs
            @@ -1,2 +1,3 @@
             public class FileProcessor {
            +    var stream = new FileStream("data.bin", FileMode.Open);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("FileStream") && f.Summary.Contains("using"));
    }

    [Fact]
    public async Task FileStreamWithUsing_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/FileProcessor.cs b/src/FileProcessor.cs
            index abc..def 100644
            --- a/src/FileProcessor.cs
            +++ b/src/FileProcessor.cs
            @@ -1,2 +1,3 @@
             public class FileProcessor {
            +    using var stream = new FileStream("data.bin", FileMode.Open);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("FileStream"));
    }

    [Fact]
    public async Task SqlConnectionWithoutUsing_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Repo.cs b/src/Repo.cs
            index abc..def 100644
            --- a/src/Repo.cs
            +++ b/src/Repo.cs
            @@ -1,2 +1,3 @@
             public class Repo {
            +    var conn = new SqlConnection(connectionString);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("SqlConnection") && f.Summary.Contains("using"));
    }

    [Fact]
    public async Task FileStreamWithDisposeInWindow_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/FileProcessor.cs b/src/FileProcessor.cs
            index abc..def 100644
            --- a/src/FileProcessor.cs
            +++ b/src/FileProcessor.cs
            @@ -1,5 +1,8 @@
             public class FileProcessor {
            +    var stream = new FileStream("data.bin", FileMode.Open);
            +    try {
            +        stream.Read(buffer, 0, 100);
            +    } finally {
            +        stream.Dispose();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("FileStream"));
    }

    [Fact]
    public async Task FactoryInjectedHttpClient_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Api.cs b/src/Api.cs
            index abc..def 100644
            --- a/src/Api.cs
            +++ b/src/Api.cs
            @@ -1,1 +1,6 @@
             public class Api {
            +    private readonly IHttpClientFactory _httpClientFactory;
            +    public Api(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }
            +    public void Do() {
            +        var client = _httpClientFactory.CreateClient("x");
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient"));
    }

    [Fact]
    public async Task MemoryStreamInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/GauntletCI.Tests/FileProcessorTests.cs b/src/GauntletCI.Tests/FileProcessorTests.cs
            index abc..def 100644
            --- a/src/GauntletCI.Tests/FileProcessorTests.cs
            +++ b/src/GauntletCI.Tests/FileProcessorTests.cs
            @@ -1,2 +1,4 @@
             public class FileProcessorTests {
            +    var ms = new MemoryStream();
            +    ms.Write(new byte[] { 1, 2, 3 }, 0, 3);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("MemoryStream"));
    }

    [Fact]
    public async Task MemoryStreamInProductionFile_ShouldFlag()
    {
        var raw = """
            diff --git a/src/DataExporter.cs b/src/DataExporter.cs
            index abc..def 100644
            --- a/src/DataExporter.cs
            +++ b/src/DataExporter.cs
            @@ -1,2 +1,3 @@
             public class DataExporter {
            +    var ms = new MemoryStream();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("MemoryStream"));
    }

    [Fact]
    public async Task SystemCommandLineCommand_ShouldNotFlag()
    {
        // System.CommandLine.Command is not IDisposable — "Command" suffix removed from heuristic
        var raw = """
            diff --git a/src/Cli/MyCommand.cs b/src/Cli/MyCommand.cs
            index abc..def 100644
            --- a/src/Cli/MyCommand.cs
            +++ b/src/Cli/MyCommand.cs
            @@ -1,2 +1,3 @@
             // cli
            +var cmd = new Command("baseline", "Manage baselines");
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Command"));
    }

    [Fact]
    public async Task SyntaxContextAllocation_ShouldNotFlag()
    {
        // SyntaxContext ends with "Context" (a DisposableSuffix) but is not IDisposable.
        var raw = """
            diff --git a/src/Rules/MyRule.cs b/src/Rules/MyRule.cs
            index abc..def 100644
            --- a/src/Rules/MyRule.cs
            +++ b/src/Rules/MyRule.cs
            @@ -1,2 +1,3 @@
             public class MyRule {
            +    var ctx = new SyntaxContext(node, semanticModel);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("SyntaxContext"));
    }

    [Fact]
    public async Task InvocationContextAllocation_ShouldNotFlag()
    {
        // InvocationContext (System.CommandLine) ends with "Context" but is not IDisposable.
        var raw = """
            diff --git a/src/Cli/MyCommand.cs b/src/Cli/MyCommand.cs
            index abc..def 100644
            --- a/src/Cli/MyCommand.cs
            +++ b/src/Cli/MyCommand.cs
            @@ -1,2 +1,3 @@
             public class MyCommand {
            +    var ctx = new InvocationContext(parseResult);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("InvocationContext"));
    }
}
