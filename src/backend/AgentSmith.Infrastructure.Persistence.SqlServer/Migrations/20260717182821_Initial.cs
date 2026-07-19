using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    JobId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogueAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DialogueJobId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnsweredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueuedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ReservedRunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InitialContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlanAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsResume = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunArtifacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCapacities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    FootprintJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCpuNanos = table.Column<long>(type: "bigint", nullable: false),
                    TotalMemBytes = table.Column<long>(type: "bigint", nullable: false),
                    Reserved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCapacities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Pipeline = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    DialogueJobId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    QuestionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemainingCommandsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionCount = table.Column<int>(type: "int", nullable: false),
                    AskedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnswerDeadlineAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Repo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunExpectations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatifiedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RatifiedBy = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RatifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditDistance = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunExpectations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunLlmCalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokensIn = table.Column<long>(type: "bigint", nullable: false),
                    TokensOut = table.Column<long>(type: "bigint", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    PromptHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CachedTokensIn = table.Column<long>(type: "bigint", nullable: false),
                    CacheCreationTokensIn = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLlmCalls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunRepos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RepoName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunRepos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepoMode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalSteps = table.Column<int>(type: "int", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CostTotalUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TokensIn = table.Column<long>(type: "bigint", nullable: false),
                    TokensOut = table.Column<long>(type: "bigint", nullable: false),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JobId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    CancelDeadlineAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProgressLedgerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptanceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSandboxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepoName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolchainImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpawnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DisposedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MemoryRequest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSandboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommandName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    ResultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecDialogSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ReposJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TranscriptJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedOutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRuns_Project_TicketId",
                table: "ActiveRuns",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAnswers_DialogueJobId_QuestionId",
                table: "DialogueAnswers",
                columns: new[] { "DialogueJobId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueuedTickets_Project_TicketId",
                table: "QueuedTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunArtifacts_RunId",
                table: "RunArtifacts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_Reserved",
                table: "RunCapacities",
                column: "Reserved");

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_RunId",
                table: "RunCapacities",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_DialogueJobId_QuestionId",
                table: "RunCheckpoints",
                columns: new[] { "DialogueJobId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_ResumedAt",
                table: "RunCheckpoints",
                column: "ResumedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_RunId",
                table: "RunCheckpoints",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunDecisions_RunId",
                table: "RunDecisions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId",
                table: "RunEvents",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunExpectations_RunId",
                table: "RunExpectations",
                column: "RunId",
                unique: true);

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
                name: "IX_SpecDialogSessions_Platform_ThreadId",
                table: "SpecDialogSessions",
                columns: new[] { "Platform", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_SessionId",
                table: "SpecDialogSessions",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveRuns");

            migrationBuilder.DropTable(
                name: "DialogueAnswers");

            migrationBuilder.DropTable(
                name: "QueuedTickets");

            migrationBuilder.DropTable(
                name: "RunArtifacts");

            migrationBuilder.DropTable(
                name: "RunCapacities");

            migrationBuilder.DropTable(
                name: "RunCheckpoints");

            migrationBuilder.DropTable(
                name: "RunDecisions");

            migrationBuilder.DropTable(
                name: "RunEvents");

            migrationBuilder.DropTable(
                name: "RunExpectations");

            migrationBuilder.DropTable(
                name: "RunLlmCalls");

            migrationBuilder.DropTable(
                name: "RunRepos");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "RunSandboxes");

            migrationBuilder.DropTable(
                name: "RunSteps");

            migrationBuilder.DropTable(
                name: "SpecDialogSessions");
        }
    }
}
