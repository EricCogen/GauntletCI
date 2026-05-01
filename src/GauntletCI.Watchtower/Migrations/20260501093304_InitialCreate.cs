using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GauntletCI.Watchtower.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationResultId = table.Column<int>(type: "INTEGER", nullable: false),
                    DraftBody = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewNotes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationResultId = table.Column<int>(type: "INTEGER", nullable: false),
                    GauntletVersionTested = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleBody = table.Column<string>(type: "TEXT", nullable: false),
                    RulesFired = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceScores = table.Column<float>(type: "REAL", nullable: false),
                    PublicationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArticleUrl = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CVEFeeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    PollingIntervalHours = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVEFeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CVEs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    CvssScore = table.Column<float>(type: "REAL", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSeenDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVEs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GauntletRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommitSha = table.Column<string>(type: "TEXT", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", nullable: false),
                    BuildSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    BuildOutput = table.Column<string>(type: "TEXT", nullable: false),
                    BuildTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CliArtifactPath = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GauntletRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Misses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationResultId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReasonForMiss = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedRuleEnhancements = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Misses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CVEFeedEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FeedId = table.Column<int>(type: "INTEGER", nullable: false),
                    CveId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CvssScore = table.Column<float>(type: "REAL", nullable: false),
                    SeverityRating = table.Column<string>(type: "TEXT", nullable: false),
                    AdvisoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CVEId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVEFeedEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CVEFeedEntries_CVEFeeds_FeedId",
                        column: x => x.FeedId,
                        principalTable: "CVEFeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CVEFeedEntries_CVEs_CVEId",
                        column: x => x.CVEId,
                        principalTable: "CVEs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedRepositoryUrls = table.Column<string>(type: "TEXT", nullable: false),
                    PatchUrls = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedVersions = table.Column<string>(type: "TEXT", nullable: false),
                    VulnerabilityClass = table.Column<string>(type: "TEXT", nullable: false),
                    AnalysisNotes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalData_CVEs_CveId",
                        column: x => x.CveId,
                        principalTable: "CVEs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GauntletRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TotalAnalyzed = table.Column<int>(type: "INTEGER", nullable: false),
                    Detected = table.Column<int>(type: "INTEGER", nullable: false),
                    Missed = table.Column<int>(type: "INTEGER", nullable: false),
                    NonAnalyzable = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticlesDrafted = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticlesPublished = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRuns_GauntletRuns_GauntletRunId",
                        column: x => x.GauntletRunId,
                        principalTable: "GauntletRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ValidationRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    CveId = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionOccurred = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfidenceScore = table.Column<float>(type: "REAL", nullable: false),
                    TriggeredRules = table.Column<string>(type: "TEXT", nullable: false),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedDiff = table.Column<string>(type: "TEXT", nullable: false),
                    StdoutPath = table.Column<string>(type: "TEXT", nullable: false),
                    StderrPath = table.Column<string>(type: "TEXT", nullable: false),
                    AnalysisStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationResults_CVEs_CveId",
                        column: x => x.CveId,
                        principalTable: "CVEs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValidationResults_ValidationRuns_ValidationRunId",
                        column: x => x.ValidationRunId,
                        principalTable: "ValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CVEFeedEntries_CVEId",
                table: "CVEFeedEntries",
                column: "CVEId");

            migrationBuilder.CreateIndex(
                name: "IX_CVEFeedEntries_FeedId_CveId",
                table: "CVEFeedEntries",
                columns: new[] { "FeedId", "CveId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CVEs_CveId",
                table: "CVEs",
                column: "CveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalData_CveId",
                table: "TechnicalData",
                column: "CveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationResults_CveId",
                table: "ValidationResults",
                column: "CveId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationResults_ValidationRunId",
                table: "ValidationResults",
                column: "ValidationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRuns_GauntletRunId",
                table: "ValidationRuns",
                column: "GauntletRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "ArticleDrafts");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "CVEFeedEntries");

            migrationBuilder.DropTable(
                name: "Misses");

            migrationBuilder.DropTable(
                name: "TechnicalData");

            migrationBuilder.DropTable(
                name: "ValidationResults");

            migrationBuilder.DropTable(
                name: "CVEFeeds");

            migrationBuilder.DropTable(
                name: "CVEs");

            migrationBuilder.DropTable(
                name: "ValidationRuns");

            migrationBuilder.DropTable(
                name: "GauntletRuns");
        }
    }
}
