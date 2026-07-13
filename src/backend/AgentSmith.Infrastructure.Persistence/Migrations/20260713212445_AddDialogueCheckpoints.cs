using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogueCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsResume",
                table: "QueuedTickets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DialogueAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DialogueJobId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    AnsweredBy = table.Column<string>(type: "TEXT", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Pipeline = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    DialogueJobId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    QuestionJson = table.Column<string>(type: "TEXT", nullable: false),
                    RemainingCommandsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AskedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AnswerDeadlineAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAnswers_DialogueJobId_QuestionId",
                table: "DialogueAnswers",
                columns: new[] { "DialogueJobId", "QuestionId" },
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogueAnswers");

            migrationBuilder.DropTable(
                name: "RunCheckpoints");

            migrationBuilder.DropColumn(
                name: "IsResume",
                table: "QueuedTickets");
        }
    }
}
