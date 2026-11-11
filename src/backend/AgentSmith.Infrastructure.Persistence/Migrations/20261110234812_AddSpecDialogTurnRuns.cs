using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogTurnRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RunningRunId",
                table: "SpecDialogSessions",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecDialogTurnProposal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ProposalJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogTurnProposal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogTurnProposal_RunId",
                table: "SpecDialogTurnProposal",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogTurnProposal_SessionId",
                table: "SpecDialogTurnProposal",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecDialogTurnProposal");

            migrationBuilder.DropColumn(
                name: "RunningRunId",
                table: "SpecDialogSessions");
        }
    }
}
