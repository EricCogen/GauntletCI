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
    public async Task EventHandlerWithoutDedup_ShouldFlag()
    {
        // Original diff structure but simpler content
        var raw = """
            diff --git a/src/EventManager.cs b/src/EventManager.cs
            index abc..def 100644
            --- a/src/EventManager.cs
            +++ b/src/EventManager.cs
            @@ -1,1 +1,1 @@
            +SomeEvent += handler;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Event handler"));
    }

    [Fact]
    public async Task EventHandlerWithMinusGuard_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/EventManager.cs b/src/EventManager.cs
            index abc..def 100644
            --- a/src/EventManager.cs
            +++ b/src/EventManager.cs
            @@ -1,5 +1,9 @@
             public class EventManager {
            +    public void RegisterHandler(EventHandler handler) {
            +        SomeEvent -= handler;
            +        SomeEvent += handler;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Event handler registered without dedup"));
    }

    [Fact]
    public async Task EventHandlerInStaticConstructor_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/EventManager.cs b/src/EventManager.cs
            index abc..def 100644
            --- a/src/EventManager.cs
            +++ b/src/EventManager.cs
            @@ -1,5 +1,9 @@
             public class EventManager {
            +    static EventManager() {
            +        // Static ctor runs exactly once - inherently idempotent
            +        SomeEvent += handler;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Event handler registered without dedup"));
    }

}

