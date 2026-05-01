// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0055Tests
{
    private static readonly GCI0055_MethodSignatureChange Rule = new();

    [Fact]
    public async Task MethodReturnTypeChanged_ShouldFire()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,5 +1,5 @@
             public class UserService {
            -    public User GetUser(int id) => _repo.Get(id);
            +    public UserDto GetUser(int id) => _mapper.Map(_repo.Get(id));
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("GetUser") && f.Summary.Contains("return type") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task MethodRequiredParameterAdded_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,5 +1,5 @@
             public class OrderService {
            -    public Order CreateOrder(CreateOrderRequest req) { }
            +    public Order CreateOrder(CreateOrderRequest req, User requestor) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("CreateOrder") && f.Summary.Contains("required parameter") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task MethodParameterAddedWithDefault_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,5 +1,5 @@
             public class PaymentService {
            -    public bool Charge(decimal amount) { }
            +    public bool Charge(decimal amount, bool async = true) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("required parameter"));
    }

    [Fact]
    public async Task MethodParameterRemoved_ShouldFire()
    {
        var raw = """
            diff --git a/src/Cache.cs b/src/Cache.cs
            index abc..def 100644
            --- a/src/Cache.cs
            +++ b/src/Cache.cs
            @@ -1,5 +1,5 @@
             public class Cache {
            -    public T Get(string key, Type hint) => _inner.Get(key, hint);
            +    public T Get(string key) => _inner.Get(key);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Get") && f.Summary.Contains("required parameter") &&
            f.Summary.Contains("removed"));
    }

    [Fact]
    public async Task PrivateMethodChanged_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Helper.cs b/src/Helper.cs
            index abc..def 100644
            --- a/src/Helper.cs
            +++ b/src/Helper.cs
            @@ -1,5 +1,5 @@
             public class Helper {
            -    private string Compute(int x) { }
            +    private string Compute(int x, int y) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task MethodInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/tests/ServiceTests.cs b/tests/ServiceTests.cs
            index abc..def 100644
            --- a/tests/ServiceTests.cs
            +++ b/tests/ServiceTests.cs
            @@ -1,5 +1,5 @@
             public class ServiceTests {
            -    public void TestMethod(Service svc) { }
            +    public void TestMethod(Service svc, string extra) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task MultipleParametersAdded_OnlyFirstRequired_ShouldFire()
    {
        var raw = """
            diff --git a/src/Query.cs b/src/Query.cs
            index abc..def 100644
            --- a/src/Query.cs
            +++ b/src/Query.cs
            @@ -1,5 +1,5 @@
             public class Query {
            -    public List<T> Execute(QueryRequest req) { }
            +    public List<T> Execute(QueryRequest req, ILogger logger, bool trace = false) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Execute") && f.Summary.Contains("ILogger"));
    }
}
