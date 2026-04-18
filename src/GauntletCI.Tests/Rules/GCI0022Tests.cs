// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0022Tests
{
    private static readonly GCI0022_IdempotencyRetrySafety Rule = new();

    [Fact]
    public async Task HttpPostWithoutIdempotencyKey_ShouldFlag()
    {
        var raw = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index abc..def 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -1,3 +1,6 @@
             public class OrderController {
            +    [HttpPost]
            +    public IActionResult Create(OrderRequest req) {
            +        _service.CreateOrder(req);
            +        return Ok();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("[HttpPost]") && f.Summary.Contains("idempotency"));
    }

    [Fact]
    public async Task HttpPostWithIdempotencyKey_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index abc..def 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -1,3 +1,8 @@
             public class OrderController {
            +    [HttpPost]
            +    public IActionResult Create([FromHeader] string IdempotencyKey, OrderRequest req) {
            +        if (_cache.TryGetValue(IdempotencyKey, out var cached)) return cached;
            +        _service.CreateOrder(req);
            +        return Ok();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("[HttpPost]"));
    }

    [Fact]
    public async Task RawInsertWithoutUpsert_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Repo.cs b/src/Repo.cs
            index abc..def 100644
            --- a/src/Repo.cs
            +++ b/src/Repo.cs
            @@ -1,2 +1,3 @@
             public class Repo {
            +    var sql = "INSERT INTO orders (id, amount) VALUES (@id, @amount)";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Raw INSERT without upsert"));
    }

    [Fact]
    public async Task InsertOrIgnore_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Repo.cs b/src/Repo.cs
            index abc..def 100644
            --- a/src/Repo.cs
            +++ b/src/Repo.cs
            @@ -1,2 +1,3 @@
             public class Repo {
            +    var sql = "INSERT OR IGNORE INTO orders (id, amount) VALUES (@id, @amount)";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Raw INSERT without upsert"));
    }

    [Fact]
    public async Task EventPlusEqualsInStaticConstructor_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/AppEvents.cs b/src/AppEvents.cs
            index abc..def 100644
            --- a/src/AppEvents.cs
            +++ b/src/AppEvents.cs
            @@ -1,3 +1,8 @@
             public class AppEvents {
            +    static AppEvents()
            +    {
            +        AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;
            +        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("deduplication"));
    }

    [Fact]
    public async Task EventPlusEqualsInStaticMethod_ShouldFlag()
    {
        // static Guid BuildId() is a static METHOD (has a return type), not a constructor.
        // The IsInsideStaticConstructor check must NOT exempt this case.
        var raw = """
            diff --git a/src/Telemetry.cs b/src/Telemetry.cs
            index abc..def 100644
            --- a/src/Telemetry.cs
            +++ b/src/Telemetry.cs
            @@ -1,5 +1,11 @@
             public class Telemetry {
            +    static Guid BuildId()
            +    {
            +        SomeEvent += Handler;
            +        SomeEvent += Handler;
            +        return Guid.NewGuid();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("deduplication") || f.RuleId == "GCI0022");
    }

    [Fact]
    public async Task EventHandlerInTestFile_ShouldNotFlag()
    {
        // Test code intentionally exercises the event pattern being tested — skip
        var raw = """
            diff --git a/src/GCI0022Tests.cs b/src/GCI0022Tests.cs
            index abc..def 100644
            --- a/src/GCI0022Tests.cs
            +++ b/src/GCI0022Tests.cs
            @@ -1,3 +1,5 @@
             public class GCI0022Tests {
            +    SomeEvent += Handler;
            +    SomeEvent += Handler;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("deduplication"));
    }
}
