using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunDefaultDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunDefaultDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    SpecId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Question = table.Column<string>(type: "TEXT", nullable: false),
                    Chosen = table.Column<string>(type: "TEXT", nullable: false),
                    AlternativesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Why = table.Column<string>(type: "TEXT", nullable: false),
                    HoldsSeries = table.Column<bool>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PointerPath = table.Column<string>(type: "TEXT", nullable: true),
                    TakenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Verdict = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, defaultValue: "pending"),
                    VerdictBy = table.Column<string>(type: "TEXT", nullable: true),
                    VerdictAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    VerdictNote = table.Column<string>(type: "TEXT", nullable: true),
                    Conversation = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDefaultDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_Conversation",
                table: "RunDefaultDecisions",
                column: "Conversation");

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_RunId",
                table: "RunDefaultDecisions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_RunId_EventSeq",
                table: "RunDefaultDecisions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunDefaultDecisions");
        }
    }
}
