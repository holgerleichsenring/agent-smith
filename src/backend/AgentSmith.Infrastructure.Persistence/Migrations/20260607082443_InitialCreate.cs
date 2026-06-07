using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Platform = table.Column<string>(type: "TEXT", nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RepoMode = table.Column<string>(type: "TEXT", nullable: true),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    CostTotalUsd = table.Column<decimal>(type: "TEXT", nullable: false),
                    TokensIn = table.Column<long>(type: "INTEGER", nullable: false),
                    TokensOut = table.Column<long>(type: "INTEGER", nullable: false),
                    CancelRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    CancelReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketLifecycles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketLifecycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActiveRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveRuns_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunArtifacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunArtifacts_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunDecisions_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Seq = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: true),
                    Phase = table.Column<string>(type: "TEXT", nullable: true),
                    Repo = table.Column<string>(type: "TEXT", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunEvents_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunLlmCalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: true),
                    Phase = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    TokensIn = table.Column<long>(type: "INTEGER", nullable: false),
                    TokensOut = table.Column<long>(type: "INTEGER", nullable: false),
                    CostUsd = table.Column<decimal>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    PromptHash = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLlmCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunLlmCalls_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunRepos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RepoName = table.Column<string>(type: "TEXT", nullable: false),
                    PrUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PrStatus = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    ChangeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunRepos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunRepos_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunSandboxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    RepoName = table.Column<string>(type: "TEXT", nullable: true),
                    ToolchainImage = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSandboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunSandboxes_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    StepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    ResultMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunSteps_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRuns_Project_TicketId",
                table: "ActiveRuns",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRuns_RunId",
                table: "ActiveRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunArtifacts_RunId",
                table: "RunArtifacts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunDecisions_RunId",
                table: "RunDecisions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId",
                table: "RunEvents",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunLlmCalls_RunId",
                table: "RunLlmCalls",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunRepos_RunId",
                table: "RunRepos",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Project",
                table: "Runs",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_RunSandboxes_RunId",
                table: "RunSandboxes",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSteps_RunId",
                table: "RunSteps",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketLifecycles_Project_Platform_TicketId",
                table: "TicketLifecycles",
                columns: new[] { "Project", "Platform", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveRuns");

            migrationBuilder.DropTable(
                name: "RunArtifacts");

            migrationBuilder.DropTable(
                name: "RunDecisions");

            migrationBuilder.DropTable(
                name: "RunEvents");

            migrationBuilder.DropTable(
                name: "RunLlmCalls");

            migrationBuilder.DropTable(
                name: "RunRepos");

            migrationBuilder.DropTable(
                name: "RunSandboxes");

            migrationBuilder.DropTable(
                name: "RunSteps");

            migrationBuilder.DropTable(
                name: "TicketLifecycles");

            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
