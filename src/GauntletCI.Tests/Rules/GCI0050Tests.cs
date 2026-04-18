// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0050Tests
{
    private static readonly GCI0050_SqlColumnTruncationRisk Rule = new();

    [Fact]
    public async Task ShortNvarcharInMigration_ShouldFire()
    {
        var raw = """
            diff --git a/src/Migrations/20240101_CreateUsers.cs b/src/Migrations/20240101_CreateUsers.cs
            index abc..def 100644
            --- a/src/Migrations/20240101_CreateUsers.cs
            +++ b/src/Migrations/20240101_CreateUsers.cs
            @@ -1,5 +1,8 @@
             migrationBuilder.CreateTable("Users", t => new {
            +    Email = t.Column<string>(type: "nvarchar(50)", nullable: false),
            +    Name  = t.Column<string>(type: "nvarchar(30)", nullable: false),
             });
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal("GCI0050", f.RuleId));
    }

    [Fact]
    public async Task ShortStringLengthAttributeInMigration_ShouldFire()
    {
        var raw = """
            diff --git a/src/Migrations/AddProfile.cs b/src/Migrations/AddProfile.cs
            index abc..def 100644
            --- a/src/Migrations/AddProfile.cs
            +++ b/src/Migrations/AddProfile.cs
            @@ -1,4 +1,6 @@
             public class UserProfile {
            +    [StringLength(20)]
            +    public string Username { get; set; }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task HasMaxLengthInDbContext_ShouldFire()
    {
        var raw = """
            diff --git a/src/Data/AppDbContext.cs b/src/Data/AppDbContext.cs
            index abc..def 100644
            --- a/src/Data/AppDbContext.cs
            +++ b/src/Data/AppDbContext.cs
            @@ -5,4 +5,5 @@
             protected override void OnModelCreating(ModelBuilder modelBuilder) {
            +    modelBuilder.Entity<User>().Property(u => u.Tag).HasMaxLength(10);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task LongNvarchar_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Migrations/20240101_CreateUsers.cs b/src/Migrations/20240101_CreateUsers.cs
            index abc..def 100644
            --- a/src/Migrations/20240101_CreateUsers.cs
            +++ b/src/Migrations/20240101_CreateUsers.cs
            @@ -1,4 +1,5 @@
             migrationBuilder.CreateTable("Users", t => new {
            +    Email = t.Column<string>(type: "nvarchar(256)", nullable: false),
             });
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ShortNvarcharInNonMigrationFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Services/UserService.cs b/src/Services/UserService.cs
            index abc..def 100644
            --- a/src/Services/UserService.cs
            +++ b/src/Services/UserService.cs
            @@ -1,4 +1,5 @@
             public class UserService {
            +    // nvarchar(50) is used in the DB schema
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ShortNvarcharInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Tests/MigrationTests.cs b/src/Tests/MigrationTests.cs
            index abc..def 100644
            --- a/src/Tests/MigrationTests.cs
            +++ b/src/Tests/MigrationTests.cs
            @@ -1,4 +1,5 @@
             public class MigrationTests {
            +    private const string ColType = "nvarchar(10)";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ShortVarcharInSqlMigration_ShouldFire()
    {
        var raw = """
            diff --git a/src/Migrations/20240301_Schema.cs b/src/Migrations/20240301_Schema.cs
            index abc..def 100644
            --- a/src/Migrations/20240301_Schema.cs
            +++ b/src/Migrations/20240301_Schema.cs
            @@ -1,4 +1,6 @@
             migrationBuilder.CreateTable("Logs", t => new {
            +    Type  = t.Column<string>(type: "varchar(30)", nullable: false),
            +    Code  = t.Column<string>(type: "varchar(50)", nullable: false),
             });
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }
}
