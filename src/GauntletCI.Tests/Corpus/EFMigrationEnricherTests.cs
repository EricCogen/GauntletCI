// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class EFMigrationEnricherTests
{
    // ── IsMigrationFilePath ───────────────────────────────────────────────────

    [Fact]
    public void IsMigrationFilePath_TimestampedFileInMigrations_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.IsMigrationFilePath(
            "src/MyApp/Migrations/20230101120000_AddUserTable.cs"));
    }

    [Fact]
    public void IsMigrationFilePath_CaseInsensitiveMigrationsFolder_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.IsMigrationFilePath(
            "src/MyApp/migrations/20230101120000_Init.cs"));
    }

    [Fact]
    public void IsMigrationFilePath_NonTimestampedFileInMigrations_ReturnsFalse()
    {
        Assert.False(EFMigrationEnricher.IsMigrationFilePath(
            "src/MyApp/Migrations/SomeMigration.cs"));
    }

    [Fact]
    public void IsMigrationFilePath_TimestampedFileOutsideMigrations_ReturnsFalse()
    {
        Assert.False(EFMigrationEnricher.IsMigrationFilePath(
            "src/MyApp/20230101120000_AddUserTable.cs"));
    }

    // ── IsSnapshotFile ────────────────────────────────────────────────────────

    [Fact]
    public void IsSnapshotFile_AppContextModelSnapshot_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.IsSnapshotFile(
            "src/MyApp/Migrations/ApplicationDbContextModelSnapshot.cs"));
    }

    [Fact]
    public void IsSnapshotFile_DbContextModelSnapshot_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.IsSnapshotFile(
            "src/Data/MyDbContextModelSnapshot.cs"));
    }

    [Fact]
    public void IsSnapshotFile_NormalFile_ReturnsFalse()
    {
        Assert.False(EFMigrationEnricher.IsSnapshotFile("src/MyApp/MyService.cs"));
    }

    // ── ContainsDdlKeyword ────────────────────────────────────────────────────

    [Fact]
    public void ContainsDdlKeyword_CreateTable_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsDdlKeyword("CREATE TABLE users (id INT)"));
    }

    [Fact]
    public void ContainsDdlKeyword_AlterTable_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsDdlKeyword("ALTER TABLE users ADD COLUMN email TEXT"));
    }

    [Fact]
    public void ContainsDdlKeyword_DropTable_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsDdlKeyword("drop table old_users"));
    }

    [Fact]
    public void ContainsDdlKeyword_PlainCode_ReturnsFalse()
    {
        Assert.False(EFMigrationEnricher.ContainsDdlKeyword("var x = table.Count;"));
    }

    // ── ContainsEfAnnotation ──────────────────────────────────────────────────

    [Fact]
    public void ContainsEfAnnotation_TableAttribute_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsEfAnnotation("[Table(\"users\")]"));
    }

    [Fact]
    public void ContainsEfAnnotation_ColumnAttribute_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsEfAnnotation("[Column(\"user_name\")]"));
    }

    [Fact]
    public void ContainsEfAnnotation_ForeignKeyAttribute_ReturnsTrue()
    {
        Assert.True(EFMigrationEnricher.ContainsEfAnnotation("[ForeignKey(\"UserId\")]"));
    }

    [Fact]
    public void ContainsEfAnnotation_PlainCode_ReturnsFalse()
    {
        Assert.False(EFMigrationEnricher.ContainsEfAnnotation("public string Name { get; set; }"));
    }

    // ── ComputeConfidence ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeConfidence_HasMigrationFile_Returns095()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: true, hasSqlFile: false, hasDdlContent: false,
            hasEfContent: false, hasSchemaAnnotation: false);
        Assert.Equal(0.95, confidence);
    }

    [Fact]
    public void ComputeConfidence_HasSqlFile_Returns085()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: false, hasSqlFile: true, hasDdlContent: false,
            hasEfContent: false, hasSchemaAnnotation: false);
        Assert.Equal(0.85, confidence);
    }

    [Fact]
    public void ComputeConfidence_HasDdlContent_Returns085()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: false, hasSqlFile: false, hasDdlContent: true,
            hasEfContent: false, hasSchemaAnnotation: false);
        Assert.Equal(0.85, confidence);
    }

    [Fact]
    public void ComputeConfidence_HasEfContent_Returns075()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: false, hasSqlFile: false, hasDdlContent: false,
            hasEfContent: true, hasSchemaAnnotation: false);
        Assert.Equal(0.75, confidence);
    }

    [Fact]
    public void ComputeConfidence_HasSchemaAnnotation_Returns075()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: false, hasSqlFile: false, hasDdlContent: false,
            hasEfContent: false, hasSchemaAnnotation: true);
        Assert.Equal(0.75, confidence);
    }

    [Fact]
    public void ComputeConfidence_NoSignals_Returns0()
    {
        var confidence = EFMigrationEnricher.ComputeConfidence(
            hasMigrationFile: false, hasSqlFile: false, hasDdlContent: false,
            hasEfContent: false, hasSchemaAnnotation: false);
        Assert.Equal(0.0, confidence);
    }

    // ── Detect end-to-end ─────────────────────────────────────────────────────

    [Fact]
    public void Detect_MigrationFilePath_HasMigrationFileAndHighConfidence()
    {
        var diff = new[]
        {
            "diff --git a/src/Data/Migrations/20230101120000_AddUserTable.cs " +
            "b/src/Data/Migrations/20230101120000_AddUserTable.cs",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.True(s.HasMigrationFile);
        Assert.True(s.MigrationDetected);
        Assert.Equal(0.95, s.MigrationConfidence);
    }

    [Fact]
    public void Detect_SqlFile_HasSqlFileAndConfidence085()
    {
        var diff = new[]
        {
            "diff --git a/db/migrations/001_init.sql b/db/migrations/001_init.sql",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.True(s.HasSqlFile);
        Assert.True(s.MigrationDetected);
        Assert.Equal(0.85, s.MigrationConfidence);
    }

    [Fact]
    public void Detect_MigrationBuilderAddedLine_HasEfContent()
    {
        var diff = new[]
        {
            "diff --git a/src/Migrations/20230101120000_Init.cs b/src/Migrations/20230101120000_Init.cs",
            "@@ -1,3 +1,5 @@",
            "+            migrationBuilder.CreateTable(",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.True(s.HasEfContent);
        Assert.True(s.MigrationDetected);
    }

    [Fact]
    public void Detect_CreateTableInAddedLine_HasDdlContentAndConfidence085()
    {
        var diff = new[]
        {
            "diff --git a/scripts/schema.sql b/scripts/schema.sql",
            "@@ -1,3 +1,5 @@",
            "+CREATE TABLE users (id INT PRIMARY KEY, name TEXT NOT NULL);",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.True(s.HasDdlContent);
        Assert.True(s.MigrationDetected);
        Assert.Equal(0.85, s.MigrationConfidence);
    }

    [Fact]
    public void Detect_TableAttributeInAddedLine_HasSchemaAnnotation()
    {
        var diff = new[]
        {
            "diff --git a/src/Models/User.cs b/src/Models/User.cs",
            "@@ -1,3 +1,5 @@",
            "+[Table(\"users\")]",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.True(s.HasSqlFile == false);
        Assert.True(s.MigrationDetected);
        Assert.Equal(0.75, s.MigrationConfidence);
    }

    [Fact]
    public void Detect_NoMigrationSignals_NotDetected()
    {
        var diff = new[]
        {
            "diff --git a/src/MyLib/MyClass.cs b/src/MyLib/MyClass.cs",
            "@@ -1,3 +1,4 @@",
            "+    public void NewMethod() {}",
        };

        var s = EFMigrationEnricher.Detect(diff);

        Assert.False(s.MigrationDetected);
        Assert.Equal(0.0, s.MigrationConfidence);
    }

    [Fact]
    public void Detect_SnapshotFile_MigrationDetected()
    {
        var diff = new[]
        {
            "diff --git a/src/Data/Migrations/ApplicationDbContextModelSnapshot.cs " +
            "b/src/Data/Migrations/ApplicationDbContextModelSnapshot.cs",
        };

        // Snapshot detection drives through the path check
        var s = EFMigrationEnricher.Detect(diff);

        // The snapshot itself is not a migration file (no timestamp) but is detected via IsSnapshotFile
        Assert.True(s.MigrationDetected);
    }
}
