// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0039Tests
{
    private static readonly GCI0039_ExternalServiceSafety Rule = new(new StubPatternProvider());

    [Fact]
    public async Task NewHttpClient_InProductionCode_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/ApiClient.cs b/src/ApiClient.cs
            index abc..def 100644
            --- a/src/ApiClient.cs
            +++ b/src/ApiClient.cs
            @@ -1,3 +1,4 @@
             public class ApiClient {
            +    var client = new HttpClient();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Direct HttpClient instantiation")
            && f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task NewHttpClient_InTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/ApiClientTests.cs b/src/ApiClientTests.cs
            index abc..def 100644
            --- a/src/ApiClientTests.cs
            +++ b/src/ApiClientTests.cs
            @@ -1,3 +1,4 @@
             public class ApiClientTests {
            +    var client = new HttpClient();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Direct HttpClient instantiation"));
    }

    [Fact]
    public async Task HttpClientWithoutTimeout_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/WeatherService.cs b/src/WeatherService.cs
            index abc..def 100644
            --- a/src/WeatherService.cs
            +++ b/src/WeatherService.cs
            @@ -1,3 +1,4 @@
             public class WeatherService {
            +    var client = new HttpClient();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("HttpClient used without explicit timeout")
            && f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task HttpClientWithTimeout_ShouldNotFlagTimeout()
    {
        var raw = """
            diff --git a/src/WeatherService.cs b/src/WeatherService.cs
            index abc..def 100644
            --- a/src/WeatherService.cs
            +++ b/src/WeatherService.cs
            @@ -1,3 +1,6 @@
             public class WeatherService {
            +    var client = new HttpClient();
            +    client.Timeout = TimeSpan.FromSeconds(30);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient used without explicit timeout"));
    }

    [Fact]
    public async Task GetAsyncWithoutCancellationToken_ShouldFlagLow()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -1,3 +1,4 @@
             public class DataService {
            +    var result = await client.GetAsync("https://api.example.com/data");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("HTTP call missing CancellationToken")
            && f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task GetAsyncWithCancellationToken_ShouldNotFlagMissingCt()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -1,3 +1,4 @@
             public class DataService {
            +    var result = await _client.GetAsync("https://api.example.com/data", cancellationToken);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HTTP call missing CancellationToken"));
    }

    [Fact]
    public async Task InjectedHttpClient_ShouldNotRequireCancellationToken()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -1,5 +1,6 @@
             public class DataService {
                 private readonly HttpClient _httpClient;
            +    var result = await _httpClient.GetAsync("https://api.example.com/data");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("HTTP call missing CancellationToken"));
    }

    [Fact]
    public async Task CleanFile_ShouldHaveNoFindings()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,4 @@
             public class UserService {
            +    return new UserDto { Name = "Alice" };
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task GrpcChannelWithHttpClient_ShouldNotFlagTimeout()
    {
        var raw = """
            diff --git a/src/GrpcService/GrpcClientConfig.cs b/src/GrpcService/GrpcClientConfig.cs
            index abc..def 100644
            --- a/src/GrpcService/GrpcClientConfig.cs
            +++ b/src/GrpcService/GrpcClientConfig.cs
            @@ -1,3 +1,6 @@
             public class GrpcClientConfig {
            +    var channel = GrpcChannel.ForAddress("https://api.example.com", new GrpcChannelOptions { MaxReceiveMessageSize = null });
            +    var handler = new HttpClientHandler();
            +    var client = new HttpClient(handler);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        // Should not flag timeout because GrpcChannel is used
        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient used without explicit timeout"));
        // But may flag direct instantiation
        var instantiationFinding = findings.FirstOrDefault(f => f.Summary.Contains("Direct HttpClient instantiation"));
        if (instantiationFinding != null)
        {
            // gRPC file, so should be skipped
            Assert.Empty(findings);
        }
    }

    [Fact]
    public async Task HttpClientWithGrpcChannelOptions_ShouldNotFlagTimeout()
    {
        var raw = """
            diff --git a/src/Config.cs b/src/Config.cs
            index abc..def 100644
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -1,3 +1,5 @@
             public class Config {
            +    var httpClientHandler = new HttpClientHandler();
            +    var httpClient = new HttpClient(httpClientHandler);
            +    var options = new GrpcChannelOptions { HttpClient = httpClient };
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        // Should not flag timeout because GrpcChannelOptions is used
        Assert.DoesNotContain(findings, f => f.Summary.Contains("HttpClient used without explicit timeout"));
    }
}
