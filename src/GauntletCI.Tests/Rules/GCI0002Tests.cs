// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0002Tests
{
    private static readonly GCI0002_GoalAlignment Rule = new();

    [Fact]
    public async Task UnclearScope_ManyFilesAcrossCategories_ShouldFlag()
    {
        // >5 files spanning frontend (.ts, .tsx), backend (.cs), config (.json), tests (test/)
        var raw = """
            diff --git a/src/Button.ts b/src/Button.ts
            index abc..def 100644
            --- a/src/Button.ts
            +++ b/src/Button.ts
            @@ -1,1 +1,2 @@
             // existing
            +const x = 1;
            diff --git a/src/Modal.tsx b/src/Modal.tsx
            index abc..def 100644
            --- a/src/Modal.tsx
            +++ b/src/Modal.tsx
            @@ -1,1 +1,2 @@
             // existing
            +const y = 2;
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +int z = 3;
            diff --git a/config/appsettings.json b/config/appsettings.json
            index abc..def 100644
            --- a/config/appsettings.json
            +++ b/config/appsettings.json
            @@ -1,1 +1,2 @@
             {}
            +{"key": "val"}
            diff --git a/tests/ServiceTests.cs b/tests/ServiceTests.cs
            index abc..def 100644
            --- a/tests/ServiceTests.cs
            +++ b/tests/ServiceTests.cs
            @@ -1,1 +1,2 @@
             // test
            +var t = new Service();
            diff --git a/src/Repository.cs b/src/Repository.cs
            index abc..def 100644
            --- a/src/Repository.cs
            +++ b/src/Repository.cs
            @@ -1,1 +1,2 @@
             // existing
            +int r = 5;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("categories"));
    }

    [Fact]
    public async Task FewFiles_ShouldNotFlagScope()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            diff --git a/src/Bar.cs b/src/Bar.cs
            index abc..def 100644
            --- a/src/Bar.cs
            +++ b/src/Bar.cs
            @@ -1,1 +1,2 @@
             // existing
            +int y = 2;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("categories"));
    }

    [Fact]
    public async Task ManyFilesWithNoCommitMessageMatch_ShouldFlagAlignment()
    {
        // >3 files, commit message with words not matching any filename
        var raw = """
            diff --git a/src/Widget.cs b/src/Widget.cs
            index abc..def 100644
            --- a/src/Widget.cs
            +++ b/src/Widget.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            diff --git a/src/Gadget.cs b/src/Gadget.cs
            index abc..def 100644
            --- a/src/Gadget.cs
            +++ b/src/Gadget.cs
            @@ -1,1 +1,2 @@
             // existing
            +int y = 2;
            diff --git a/src/Thingamajig.cs b/src/Thingamajig.cs
            index abc..def 100644
            --- a/src/Thingamajig.cs
            +++ b/src/Thingamajig.cs
            @@ -1,1 +1,2 @@
             // existing
            +int z = 3;
            diff --git a/src/Doohickey.cs b/src/Doohickey.cs
            index abc..def 100644
            --- a/src/Doohickey.cs
            +++ b/src/Doohickey.cs
            @@ -1,1 +1,2 @@
             // existing
            +int w = 4;
            """;

        var diff = DiffParser.Parse(raw, commitMessage: "fix authentication vulnerability");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("unrelated"));
    }
}
