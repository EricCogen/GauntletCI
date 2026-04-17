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
            (f.Summary.Contains("GetUser") || f.Summary.Contains("DeleteUser")) &&
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
}
