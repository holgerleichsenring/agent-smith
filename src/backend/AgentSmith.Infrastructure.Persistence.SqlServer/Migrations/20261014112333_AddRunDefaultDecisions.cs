using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Chosen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlternativesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Why = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HoldsSeries = table.Column<bool>(type: "bit", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PointerPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TakenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "pending"),
                    VerdictBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerdictAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerdictNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Conversation = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                unique: true,
                filter: "[EventSeq] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunDefaultDecisions");
        }
    }
}
