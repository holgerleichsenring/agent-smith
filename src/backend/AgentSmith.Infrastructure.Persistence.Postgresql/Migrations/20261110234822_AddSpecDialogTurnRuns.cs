using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
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
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecDialogTurnProposal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ProposalJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
