using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                type: "nvarchar(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecDialogTurnProposal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ProposalJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
