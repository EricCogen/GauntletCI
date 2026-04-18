// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0045Tests
{
    private static readonly GCI0045_ComplexityControl Rule = new();

    [Fact]
    public async Task EmptyDiff_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    public string GetUser(int id) => _repo.Get(id)?.Name ?? "unknown";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NewInterfaceWithSingleImplementor_ShouldFire()
    {
        var raw = """
            diff --git a/src/IOrderService.cs b/src/IOrderService.cs
            index abc..def 100644
            --- a/src/IOrderService.cs
            +++ b/src/IOrderService.cs
            @@ -0,0 +1,4 @@
            +public interface IOrderService {
            +    Task<Order> GetOrderAsync(int id);
            +}
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -0,0 +1,5 @@
            +public class OrderService : IOrderService {
            +    public async Task<Order> GetOrderAsync(int id) {
            +        return await _repo.GetAsync(id);
            +    }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("IOrderService"));
    }

    [Fact]
    public async Task InterfaceWithMultipleImplementors_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/IPaymentGateway.cs b/src/IPaymentGateway.cs
            index abc..def 100644
            --- a/src/IPaymentGateway.cs
            +++ b/src/IPaymentGateway.cs
            @@ -0,0 +1,3 @@
            +public interface IPaymentGateway {
            +    Task ChargeAsync(decimal amount);
            +}
            diff --git a/src/StripeGateway.cs b/src/StripeGateway.cs
            index abc..def 100644
            --- a/src/StripeGateway.cs
            +++ b/src/StripeGateway.cs
            @@ -0,0 +1,3 @@
            +public class StripeGateway : IPaymentGateway {
            +    public async Task ChargeAsync(decimal amount) { }
            +}
            diff --git a/src/PayPalGateway.cs b/src/PayPalGateway.cs
            index abc..def 100644
            --- a/src/PayPalGateway.cs
            +++ b/src/PayPalGateway.cs
            @@ -0,0 +1,3 @@
            +public class PayPalGateway : IPaymentGateway {
            +    public async Task ChargeAsync(decimal amount) { }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("IPaymentGateway"));
    }

    [Fact]
    public async Task AbstractClassWithNoAbstractMembers_ShouldFire()
    {
        var raw = """
            diff --git a/src/BaseHandler.cs b/src/BaseHandler.cs
            index abc..def 100644
            --- a/src/BaseHandler.cs
            +++ b/src/BaseHandler.cs
            @@ -0,0 +1,7 @@
            +public abstract class BaseHandler {
            +    protected readonly ILogger _logger;
            +    protected BaseHandler(ILogger logger) {
            +        _logger = logger;
            +    }
            +    public void LogStart() => _logger.Log("start");
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("abstract class") || f.Summary.Contains("Abstract class"));
    }

    [Fact]
    public async Task AbstractClassWithAbstractMembers_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/BaseProcessor.cs b/src/BaseProcessor.cs
            index abc..def 100644
            --- a/src/BaseProcessor.cs
            +++ b/src/BaseProcessor.cs
            @@ -0,0 +1,5 @@
            +public abstract class BaseProcessor {
            +    public abstract Task ProcessAsync(Item item);
            +    public void LogStart() { }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("no abstract members") || f.Evidence.Contains("no abstract member"));
    }

    [Fact]
    public async Task PassiveDelegationWrapper_ShouldFire()
    {
        var raw = """
            diff --git a/src/LoggingOrderService.cs b/src/LoggingOrderService.cs
            index abc..def 100644
            --- a/src/LoggingOrderService.cs
            +++ b/src/LoggingOrderService.cs
            @@ -0,0 +1,9 @@
            +public class LoggingOrderService : IOrderService {
            +    private readonly IOrderService _inner;
            +    public Task<Order> GetOrderAsync(int id) {
            +        return _inner.GetOrderAsync(id);
            +    }
            +    public Task<bool> DeleteOrderAsync(int id) {
            +        return _inner.DeleteOrderAsync(id);
            +    }
            +    public Task<Order> CreateOrderAsync(Order o) {
            +        return _inner.CreateOrderAsync(o);
            +    }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("delegation") || f.Summary.Contains("wrapper"));
    }

    [Fact]
    public async Task DelegationWrapperInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/GauntletCI.Tests/FakeServiceTests.cs b/src/GauntletCI.Tests/FakeServiceTests.cs
            index abc..def 100644
            --- a/src/GauntletCI.Tests/FakeServiceTests.cs
            +++ b/src/GauntletCI.Tests/FakeServiceTests.cs
            @@ -1,3 +1,9 @@
             public class FakeService {
            +    public string GetA() => return _inner.GetA();
            +    public string GetB() => return _inner.GetB();
            +    public string GetC() => return _inner.GetC();
            +    public string GetD() => return _inner.GetD();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("delegation") || f.Summary.Contains("wrapper"));
    }
}
