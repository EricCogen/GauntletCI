using System.Diagnostics;
using Xunit;

namespace GauntletCI.Tests.FAQ;

/// <summary>
/// Validates that GauntletCI output matches the documented examples in README.md
/// 
/// These tests verify that the specific findings and output formats shown in the
/// README documentation are actually produced by the GauntletCI analyzer.
/// 
/// Documented examples:
/// - [High] GCI0003: Guard clause removed
/// - GCI0007: Error Handling Integrity (Silent catch blocks)
/// - GCI0012: Secret Hygiene (Hardcoded API keys)
/// - GCI0004: Public API Contract (CancellationToken changes)
/// - GCI0029: PII Logging Leak (Email logging)
/// - GCI0016: Concurrency Safety (Static counter without sync)
/// - GCI0032: Unhandled Exception Paths
/// - GCI0014: Dependency version shift
/// - GCI0009: Syntax error handling
/// </summary>
public class ReadmeOutputExamplesTests
{
    private const string GauntletCiRepo = @"C:\Users\ericc\GauntletCI";

    /// <summary>
    /// README example (lines 79-83):
    /// [High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
    /// longer thrown on null input. Callers relying on this contract will see
    /// NullReferenceException deeper in the call stack.
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0003_GuardClauseRemovalExample_ProducesDocumentedOutput()
    {
        // Create a temporary repo with the documented change
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Initialize git repo
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            // Create the original file
            var servicePath = Path.Combine(testDir, "Service.cs");
            var originalCode = """
                public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
                {
                    if (request is null) throw new ArgumentNullException(nameof(request));
                    var order = new Order(request.CustomerId, request.Items);
                    return await _repo.SaveAsync(order);
                }
                """;

            File.WriteAllText(servicePath, originalCode);
            await RunGit(testDir, "add", "Service.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Modify to remove the guard clause
            var modifiedCode = """
                public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
                {
                    var order = new Order(request.CustomerId, request.Items);
                    return await _repo.SaveAsync(order);
                }
                """;

            File.WriteAllText(servicePath, modifiedCode);
            await RunGit(testDir, "add", "Service.cs");

            // Run GauntletCI and capture output
            var output = await RunGauntletCI(testDir, "analyze --staged");

            // Verify the output contains the expected finding
            Assert.Contains("GCI0003", output);
            Assert.Contains("Guard clause removed", output);
            Assert.Contains("[High]", output);
            Assert.Contains("ArgumentNullException", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README Tier 1 Scenario 02:
    /// "Silent `catch { }` around payment call" -> GCI0007 Error Handling Integrity
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0007_SilentCatchBlock_ProducesDocumentedOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            // Create original code without silent catch
            var paymentPath = Path.Combine(testDir, "Payment.cs");
            var originalCode = """
                public void ProcessPayment(Order order)
                {
                    var result = _paymentGateway.Charge(order.Amount);
                    _logger.LogInformation("Payment processed");
                }
                """;

            File.WriteAllText(paymentPath, originalCode);
            await RunGit(testDir, "add", "Payment.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Modify to add silent catch block
            var modifiedCode = """
                public void ProcessPayment(Order order)
                {
                    try
                    {
                        var result = _paymentGateway.Charge(order.Amount);
                        _logger.LogInformation("Payment processed");
                    }
                    catch { }
                }
                """;

            File.WriteAllText(paymentPath, modifiedCode);
            await RunGit(testDir, "add", "Payment.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0007", output);
            Assert.Contains("Error Handling", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README Tier 1 Scenario 03:
    /// "Hardcoded API key in Program.cs" -> GCI0012 Secret Hygiene
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0012_HardcodedApiKey_ProducesDocumentedOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            var programPath = Path.Combine(testDir, "Program.cs");
            var originalCode = """
                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();
                app.Run();
                """;

            File.WriteAllText(programPath, originalCode);
            await RunGit(testDir, "add", "Program.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Add hardcoded API key
            var modifiedCode = """
                var builder = WebApplication.CreateBuilder(args);
                var apiKey = "sk-1234567890abcdefghij";
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                var app = builder.Build();
                app.Run();
                """;

            File.WriteAllText(programPath, modifiedCode);
            await RunGit(testDir, "add", "Program.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0012", output);
            Assert.Contains("Secret", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README Tier 1 Scenario 04:
    /// "CancellationToken dropped from IPaymentClient" -> GCI0004 Public API Contract
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0004_CancellationTokenDropped_ProducesDocumentedOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            var interfacePath = Path.Combine(testDir, "IPaymentClient.cs");
            var originalCode = """
                public interface IPaymentClient
                {
                    Task<PaymentResult> ChargeAsync(decimal amount, CancellationToken ct);
                }
                """;

            File.WriteAllText(interfacePath, originalCode);
            await RunGit(testDir, "add", "IPaymentClient.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Remove CancellationToken parameter
            var modifiedCode = """
                public interface IPaymentClient
                {
                    Task<PaymentResult> ChargeAsync(decimal amount);
                }
                """;

            File.WriteAllText(interfacePath, modifiedCode);
            await RunGit(testDir, "add", "IPaymentClient.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0004", output);
            Assert.Contains("Contract", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README Tier 1 Scenario 05:
    /// "Customer email logged in LogInformation" -> GCI0029 PII Logging Leak
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0029_PiiLoggingLeak_ProducesDocumentedOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            var servicePath = Path.Combine(testDir, "OrderService.cs");
            var originalCode = """
                public void ProcessOrder(Order order)
                {
                    _logger.LogInformation("Processing order {OrderId}", order.Id);
                }
                """;

            File.WriteAllText(servicePath, originalCode);
            await RunGit(testDir, "add", "OrderService.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Add PII to logging
            var modifiedCode = """
                public void ProcessOrder(Order order)
                {
                    _logger.LogInformation("Processing order {OrderId} for {Email}", order.Id, order.Customer.Email);
                }
                """;

            File.WriteAllText(servicePath, modifiedCode);
            await RunGit(testDir, "add", "OrderService.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0029", output);
            Assert.Contains("PII", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README Tier 1 Scenario 06:
    /// "Static counter mutated without sync" -> GCI0016 Concurrency Safety
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0016_UnsynchronizedStaticMutation_ProducesDocumentedOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            var statsPath = Path.Combine(testDir, "Stats.cs");
            var originalCode = """
                public class Stats
                {
                    private static int _requestCount = 0;
                }
                """;

            File.WriteAllText(statsPath, originalCode);
            await RunGit(testDir, "add", "Stats.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Add unsynchronized mutation
            var modifiedCode = """
                public class Stats
                {
                    private static int _requestCount = 0;
                    
                    public void IncrementRequestCount()
                    {
                        _requestCount++;
                    }
                }
                """;

            File.WriteAllText(statsPath, modifiedCode);
            await RunGit(testDir, "add", "Stats.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0016", output);
            Assert.Contains("Concurrency", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// README FAQ states:
    /// "[WARNING] GCI0009: Syntactically invalid code detected in file. 
    /// Structural validation skipped. Commit permitted but unverified."
    /// </summary>
    [Fact(Skip = "Requires integration with actual GCI binary - run manually")]
    public async Task GCI0009_SyntaxError_ProducesWarningOutput()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"gci-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            await RunGit(testDir, "init");
            await RunGit(testDir, "config", "user.email", "test@example.com");
            await RunGit(testDir, "config", "user.name", "Test User");

            var codePath = Path.Combine(testDir, "Broken.cs");
            var originalCode = "public class Valid { }";

            File.WriteAllText(codePath, originalCode);
            await RunGit(testDir, "add", "Broken.cs");
            await RunGit(testDir, "commit", "-m", "Initial commit");

            // Introduce syntax error
            var brokenCode = """
                public class Broken {
                    public void Method( {
                        // missing parameter type
                    }
                }
                """;

            File.WriteAllText(codePath, brokenCode);
            await RunGit(testDir, "add", "Broken.cs");

            var output = await RunGauntletCI(testDir, "analyze --staged");

            Assert.Contains("GCI0009", output);
            Assert.Contains("Syntactically invalid", output);
            Assert.Contains("WARNING", output);
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// Helper: Run git command in a directory
    /// </summary>
    private static async Task RunGit(string workingDir, params string[] args)
    {
        var process = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = string.Join(" ", args),
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(process);
        await proc!.WaitForExitAsync();
        
        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Git failed: {error}");
        }
    }

    /// <summary>
    /// Helper: Run GauntletCI and capture output
    /// </summary>
    private static async Task<string> RunGauntletCI(string workingDir, string arguments)
    {
        var process = new ProcessStartInfo
        {
            FileName = "gauntletci",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(process);
        var output = await proc!.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        
        return output + error;
    }
}
