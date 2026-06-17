// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0024Tests
{
    private static readonly GCI0024_ResourceLifecycle Rule = new(new StubPatternProvider());

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
            diff --git a/tests/GauntletCI.Tests/FileProcessorTests.cs b/tests/GauntletCI.Tests/FileProcessorTests.cs
            index abc..def 100644
            --- a/tests/GauntletCI.Tests/FileProcessorTests.cs
            +++ b/tests/GauntletCI.Tests/FileProcessorTests.cs
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
        // System.CommandLine.Command is not IDisposable: "Command" suffix removed from heuristic
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
            +    public void Do() { var ctx = new SyntaxContext(node, semanticModel); }
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
            +    public void Do() { var ctx = new InvocationContext(parseResult); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null); Assert.DoesNotContain(findings, f => f.Summary.Contains("InvocationContext"));
    }

    [Fact]
    public async Task NewHttpClientWithoutUsing_GCI0039OwnsIt_ShouldNotFlag()
    {
        // GCI0039 (External Service Safety) is the authoritative reporter for new HttpClient().
        // GCI0024 must not double-report the same instantiation.
        var raw = """
            diff --git a/src/ApiService.cs b/src/ApiService.cs
            index abc..def 100644
            --- a/src/ApiService.cs
            +++ b/src/ApiService.cs
            @@ -1,2 +1,3 @@
             public class ApiService {
            +    var client = new HttpClient();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient"));
    }

    [Fact]
    public async Task LoggingAdapterScope_ShouldNotFlag()
    {
        // LoggingAdapterScope: short-lived diagnostic scopes are managed at higher level.
        var raw = """
            diff --git a/src/Diagnostics.cs b/src/Diagnostics.cs
            index abc..def 100644
            --- a/src/Diagnostics.cs
            +++ b/src/Diagnostics.cs
            @@ -1,2 +1,3 @@
             public class Diagnostics {
            +    var scope = new LoggingAdapterScope();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("LoggingAdapterScope"));
    }

    [Fact]
    public async Task EnumeratorType_ShouldNotFlag()
    {
        // Enumerator types: typically short-lived value types or immediately consumed.
        var raw = """
            diff --git a/src/Collections.cs b/src/Collections.cs
            index abc..def 100644
            --- a/src/Collections.cs
            +++ b/src/Collections.cs
            @@ -1,2 +1,3 @@
             public class Collections {
            +    var enumerator = new WhiteSpaceSegmentEnumerator();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Enumerator"));
    }

    [Fact]
    public async Task MultiLineConstructorArg_SourceStreamPassedToArchive_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/SharpCompress/Archives/GZip/GZipArchive.Factory.cs b/src/SharpCompress/Archives/GZip/GZipArchive.Factory.cs
            index abc..def 100644
            --- a/src/SharpCompress/Archives/GZip/GZipArchive.Factory.cs
            +++ b/src/SharpCompress/Archives/GZip/GZipArchive.Factory.cs
            @@ -48,7 +48,7 @@ public static IWritableArchive<GZipWriterOptions> OpenArchive(
                 return new GZipArchive(
                     new SourceStream(
                         fileInfo,
                         i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
            -                readerOptions ?? new ReaderOptions()
            +                readerOptions ?? ReaderOptions.ForFilePath
                     )
                 );
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("SourceStream"));
    }

    [Fact]
    public async Task FieldInitializerTelemetryClient_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Handler.cs b/src/Handler.cs
            index abc..def 100644
            --- a/src/Handler.cs
            +++ b/src/Handler.cs
            @@ -18,3 +18,3 @@ internal partial class Handler
            -    internal ITelemetryClient _telemetryClient = new TelemetryClient();
            +    internal ITelemetryClient TelemetryClient = new TelemetryClient();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("TelemetryClient"));
    }

    [Fact]
    public async Task SharpCompressPr1243Patch_ShouldNotFlagFactorySourceStream()
    {
        var patchPath = Path.Combine(
            FindRepoRoot(),
            "data",
            "fixtures",
            "discovery",
            "adamhathcock_sharpcompress_pr1243",
            "diff.patch");
        Assert.True(File.Exists(patchPath), patchPath);

        var diff = DiffParser.Parse(await File.ReadAllTextAsync(patchPath));
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("SourceStream", StringComparison.Ordinal)
            && f.Evidence.Contains("Factory.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AzureAdPr3410Patch_ShouldNotFlagTelemetryClientFieldInitializer()
    {
        var patchPath = Path.Combine(
            FindRepoRoot(),
            "data",
            "fixtures",
            "discovery",
            "azuread_azure-activedirectory-identitymodel-extensions-for-dotnet_pr3410",
            "diff.patch");
        Assert.True(File.Exists(patchPath), patchPath);

        var diff = DiffParser.Parse(await File.ReadAllTextAsync(patchPath));
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("TelemetryClient", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GauntletCI.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate GauntletCI repo root.");
    }
}
