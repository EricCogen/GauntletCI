// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0014Tests
{
    private static readonly GCI0014_RollbackSafety Rule = new();

    [Fact]
    public async Task DropTableStatement_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/Migration001.cs b/src/Migration001.cs
            index abc..def 100644
            --- a/src/Migration001.cs
            +++ b/src/Migration001.cs
            @@ -1,1 +1,2 @@
             // migration
            +DROP TABLE Users
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("DROP TABLE") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task FileDeleteCall_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Cleanup.cs b/src/Cleanup.cs
            index abc..def 100644
            --- a/src/Cleanup.cs
            +++ b/src/Cleanup.cs
            @@ -1,1 +1,2 @@
             // cleanup
            +File.Delete(path);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("File.Delete("));
    }

    [Fact]
    public async Task MigrationWithoutDown_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Migrations/AddUsersMigration.cs b/src/Migrations/AddUsersMigration.cs
            index abc..def 100644
            --- a/src/Migrations/AddUsersMigration.cs
            +++ b/src/Migrations/AddUsersMigration.cs
            @@ -1,1 +1,5 @@
             // migration
            +public class AddUsersMigration
            +{
            +    public void Up(MigrationBuilder builder) { }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("no Down()") || f.Evidence.Contains("Down() not found"));
    }

    [Fact]
    public async Task MigrationWithDown_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Migrations/AddUsersMigration.cs b/src/Migrations/AddUsersMigration.cs
            index abc..def 100644
            --- a/src/Migrations/AddUsersMigration.cs
            +++ b/src/Migrations/AddUsersMigration.cs
            @@ -1,1 +1,7 @@
             // migration
            +public class AddUsersMigration
            +{
            +    public void Up(MigrationBuilder builder) { }
            +    public void Down(MigrationBuilder builder) { }
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("no Down()") || f.Evidence.Contains("Down() not found"));
    }

    [Fact]
    public async Task AlterTableStatement_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Migration002.cs b/src/Migration002.cs
            index abc..def 100644
            --- a/src/Migration002.cs
            +++ b/src/Migration002.cs
            @@ -1,1 +1,2 @@
             // migration
            +ALTER TABLE Orders DROP COLUMN Status
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Confidence == Confidence.High);
    }
}
