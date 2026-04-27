// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0047Tests
{
    private static readonly GCI0047_NamingContractAlignment Rule = new();

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
    public async Task GetToDelete_Rename_ShouldFire()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -3,5 +3,5 @@
            -    public User GetUser(int id)
            -    {
            -        return _repo.Find(id);
            -    }
            +    public User DeleteUser(int id)
            +    {
            +        return _repo.Remove(id);
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Contradictory") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task GetToUpdate_Rename_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -3,5 +3,5 @@
            -    public User GetUser(int id)
            -    {
            -        return _repo.Find(id);
            -    }
            +    public User UpdateUser(int id)
            +    {
            +        return _repo.Update(id);
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task IsEnabledToIsDisabled_ShouldFire()
    {
        var raw = """
            diff --git a/src/FeatureToggle.cs b/src/FeatureToggle.cs
            index abc..def 100644
            --- a/src/FeatureToggle.cs
            +++ b/src/FeatureToggle.cs
            @@ -1,4 +1,4 @@
             public class FeatureToggle {
            -    public bool IsEnabled { get; set; }
            +    public bool IsDisabled { get; set; }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("IsEnabled") || f.Summary.Contains("IsDisabled") ||
            f.Summary.Contains("Boolean naming inversion"));
    }

    [Fact]
    public async Task BothBooleanSymbolsOnBothSides_ShouldNotFire()
    {
        // Regression: adding `readonly` to both IsValid and IsInvalid was a false positive
        // because both symbols appeared in removed AND added lines simultaneously.
        var raw = """
            diff --git a/src/Handle.cs b/src/Handle.cs
            index abc..def 100644
            --- a/src/Handle.cs
            +++ b/src/Handle.cs
            @@ -1,5 +1,5 @@
             public class Handle {
            -    public bool IsInvalid => this.Handle == IntPtr.Zero;
            +    public readonly bool IsInvalid => this.Handle == IntPtr.Zero;
            -    public bool IsValid => this.Handle != IntPtr.Zero;
            +    public readonly bool IsValid => this.Handle != IntPtr.Zero;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Boolean naming inversion"));
    }

    [Fact]
    public async Task UnrelatedRenames_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -3,5 +3,5 @@
            -    public Order FetchOrder(int id)
            -    {
            -        return _repo.Get(id);
            -    }
            +    public Order LoadOrder(int id)
            +    {
            +        return _repo.Get(id);
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task FindToDelete_Rename_ShouldFire()
    {
        var raw = """
            diff --git a/src/SearchService.cs b/src/SearchService.cs
            index abc..def 100644
            --- a/src/SearchService.cs
            +++ b/src/SearchService.cs
            @@ -3,5 +3,5 @@
            -    public Record FindRecord(int id)
            -    {
            -        return _store.Get(id);
            -    }
            +    public Record DeleteRecord(int id)
            +    {
            +        return _store.Remove(id);
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task LoadToRemove_Rename_ShouldFire()
    {
        var raw = """
            diff --git a/src/CacheService.cs b/src/CacheService.cs
            index abc..def 100644
            --- a/src/CacheService.cs
            +++ b/src/CacheService.cs
            @@ -3,5 +3,5 @@
            -    public Data LoadCache(string key)
            -    {
            -        return _cache[key];
            -    }
            +    public Data RemoveCache(string key)
            +    {
            -        _cache.Remove(key);
            +        _cache.Remove(key);
            +        return null;
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task TestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Tests/UserServiceTests.cs b/src/Tests/UserServiceTests.cs
            index abc..def 100644
            --- a/src/Tests/UserServiceTests.cs
            +++ b/src/Tests/UserServiceTests.cs
            @@ -3,5 +3,5 @@
            -    public void GetUser_Test()
            -    {
            -    }
            +    public void DeleteUser_Test()
            +    {
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task AddToRemove_Rename_ShouldFire()
    {
        var raw = """
            diff --git a/src/CartService.cs b/src/CartService.cs
            index abc..def 100644
            --- a/src/CartService.cs
            +++ b/src/CartService.cs
            @@ -3,5 +3,5 @@
            -    public void AddItem(CartItem item)
            -    {
            -        _items.Add(item);
            -    }
            +    public void RemoveItem(CartItem item)
            +    {
            +        _items.Remove(item);
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("AddItem") || f.Summary.Contains("RemoveItem") ||
            f.Summary.Contains("Contradictory"));
    }

    [Fact]
    public async Task BothVerbsOnBothSides_Reformatting_ShouldNotFire()
    {
        // Regression: reformatting (e.g. to expression-body) leaves both Add(...) and Remove(...)
        // in removed AND added lines. The rule must not interpret this as a contradictory rename.
        var raw = """
            diff --git a/src/ReadOnlyCollection.cs b/src/ReadOnlyCollection.cs
            index abc..def 100644
            --- a/src/ReadOnlyCollection.cs
            +++ b/src/ReadOnlyCollection.cs
            @@ -1,8 +1,4 @@
            -    public virtual void Add(string key, object? value)
            -    {
            -        throw new NotSupportedException();
            -    }
            +    public virtual void Add(string key, object? value) => throw new NotSupportedException();
            -    public virtual bool Remove(string key)
            -    {
            -        return false;
            -    }
            +    public virtual bool Remove(string key) => false;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Contradictory"));
    }
}
