// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0036Tests
{
    private static readonly GCI0036_PureContextMutation Rule = new(new StubPatternProvider());

    [Fact]
    public async Task AssignmentInGetter_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,7 +1,8 @@
             public class Foo {
                 private int _count;
                 public int Count {
                     get {
            +            _count = 42;
                         return _count;
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("getter") || f.Summary.Contains("pure"));
    }

    [Fact]
    public async Task AssignmentInPureMethod_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,5 +1,7 @@
             public class Calculator {
                 private int _cache;
                 [Pure]
                 public int Compute(int x) {
            +        _cache = x * 2;
                     return x * 2;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("getter") || f.Summary.Contains("pure") || f.Summary.Contains("Pure"));
    }

    [Fact]
    public async Task GetterWithoutAssignment_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,7 +1,8 @@
             public class Foo {
                 private int _count = 5;
                 public int Count {
                     get {
            +            return _count + 1;
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task AssignmentOutsideGetter_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,4 +1,5 @@
             public class Foo {
                 private int _count;
                 public void SetCount(int value) {
            +        _count = value;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task LocalVarDeclarationInGetter_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,6 +1,7 @@
             public class Foo {
                 private int _count;
                 public int Count {
                     get {
            +            var result = _count * 2;
                         return result;
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ForLoopVarInGetter_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,6 +1,7 @@
             public class Foo {
                 private List<int> _items;
                 public int Count {
                     get {
            +            for (var i = 0; i < _items.Count; i++) { }
                         return _items.Count;
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task TypeDeclarationInPureMethod_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,5 +1,7 @@
             public class Foo {
                 [Pure]
                 public Dictionary<string, string> GetMap() {
            +        Dictionary<string, string> result = new();
            +        string key = "default";
                     return result;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task AssignmentInGetterInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/GauntletCI.Tests/FooTests.cs b/src/GauntletCI.Tests/FooTests.cs
            index abc..def 100644
            --- a/src/GauntletCI.Tests/FooTests.cs
            +++ b/src/GauntletCI.Tests/FooTests.cs
            @@ -1,7 +1,8 @@
             public class FooTests {
                 private int _count;
                 public int Count {
                     get {
            +            _count = 42;
                         return _count;
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("mutation") || f.Summary.Contains("getter"));
    }
}
