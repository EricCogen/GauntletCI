// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0041Tests
{
    private static readonly GCI0041_TestQualityGaps Rule = new();

    [Fact]
    public async Task FactSkipInTestFile_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/OrderTests.cs b/src/OrderTests.cs
            index abc..def 100644
            --- a/src/OrderTests.cs
            +++ b/src/OrderTests.cs
            @@ -1,3 +1,8 @@
             public class OrderTests {
            +    [Fact(Skip = "broken")]
            +    public async Task ProcessOrder_ShouldSucceed()
            +    {
            +        Assert.True(true);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("silenced") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task IgnoreAttributeInTestFile_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/PaymentTests.cs b/src/PaymentTests.cs
            index abc..def 100644
            --- a/src/PaymentTests.cs
            +++ b/src/PaymentTests.cs
            @@ -1,3 +1,8 @@
             public class PaymentTests {
            +    [Ignore]
            +    [Test]
            +    public void Process_ShouldSucceed()
            +    {
            +        Assert.IsTrue(true);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("silenced") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task SkipInNonTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,5 @@
             public class OrderService {
            +    [Fact(Skip = "broken")]
            +    public void Foo() { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("silenced"));
    }

    [Fact]
    public async Task UninformativeTestName_Test1_ShouldFlagLow()
    {
        var raw = """
            diff --git a/src/CartTests.cs b/src/CartTests.cs
            index abc..def 100644
            --- a/src/CartTests.cs
            +++ b/src/CartTests.cs
            @@ -1,3 +1,8 @@
             public class CartTests {
            +    [Fact]
            +    public void Test1()
            +    {
            +        Assert.True(true);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Uninformative") &&
            f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task DescriptiveTestName_ShouldNotFlagUninformative()
    {
        var raw = """
            diff --git a/src/CartTests.cs b/src/CartTests.cs
            index abc..def 100644
            --- a/src/CartTests.cs
            +++ b/src/CartTests.cs
            @@ -1,3 +1,8 @@
             public class CartTests {
            +    [Fact]
            +    public void ShouldReturnOk_WhenValid()
            +    {
            +        Assert.True(true);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Uninformative"));
    }

    [Fact]
    public async Task TestMethodWithNoAssertions_ShouldFlagLow()
    {
        var raw = """
            diff --git a/src/InvoiceTests.cs b/src/InvoiceTests.cs
            index abc..def 100644
            --- a/src/InvoiceTests.cs
            +++ b/src/InvoiceTests.cs
            @@ -1,3 +1,8 @@
             public class InvoiceTests {
            +    [Fact]
            +    public async Task GetInvoice_ReturnsResult()
            +    {
            +        var result = await _svc.GetInvoiceAsync(1);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("assertions") &&
            f.Confidence == Confidence.Low);
    }

    [Fact]
    public async Task TestMethodWithAssertEqual_ShouldNotFlagEmptyAssertions()
    {
        var raw = """
            diff --git a/src/InvoiceTests.cs b/src/InvoiceTests.cs
            index abc..def 100644
            --- a/src/InvoiceTests.cs
            +++ b/src/InvoiceTests.cs
            @@ -1,3 +1,9 @@
             public class InvoiceTests {
            +    [Fact]
            +    public async Task GetInvoice_ReturnsCorrectAmount()
            +    {
            +        var result = await _svc.GetInvoiceAsync(1);
            +        Assert.Equal(100m, result.Amount);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("assertions"));
    }

    [Fact]
    public async Task TestWithCustomAssertHelper_ShouldNotFlagEmptyAssertions()
    {
        var raw = """
            diff --git a/src/OrderTests.cs b/src/OrderTests.cs
            index abc..def 100644
            --- a/src/OrderTests.cs
            +++ b/src/OrderTests.cs
            @@ -1,3 +1,9 @@
             public class OrderTests {
            +    [Fact]
            +    public async Task PlaceOrder_ShouldSucceed()
            +    {
            +        var result = await _svc.PlaceOrderAsync(new Order());
            +        AssertValidOrder(result);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("assertions"));
    }

    [Fact]
    public async Task TestWithFluentMust_ShouldNotFlagEmptyAssertions()
    {
        var raw = """
            diff --git a/src/PaymentTests.cs b/src/PaymentTests.cs
            index abc..def 100644
            --- a/src/PaymentTests.cs
            +++ b/src/PaymentTests.cs
            @@ -1,3 +1,9 @@
             public class PaymentTests {
            +    [Fact]
            +    public async Task Charge_ShouldSucceed()
            +    {
            +        var result = await _svc.ChargeAsync(100m);
            +        result.Success.Must(BeTrue, "payment must succeed");
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("assertions"));
    }

    [Fact]
    public async Task CleanTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserTests.cs b/src/UserTests.cs
            index abc..def 100644
            --- a/src/UserTests.cs
            +++ b/src/UserTests.cs
            @@ -1,3 +1,9 @@
             public class UserTests {
            +    [Fact]
            +    public async Task GetUser_ReturnsCorrectName()
            +    {
            +        var user = await _svc.GetUserAsync(1);
            +        Assert.Equal("Alice", user.Name);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
