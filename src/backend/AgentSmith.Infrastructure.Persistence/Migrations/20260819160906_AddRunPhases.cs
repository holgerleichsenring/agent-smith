using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunPhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhaseId",
                table: "RunSteps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhaseId",
                table: "RunDecisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunPhases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    PhaseId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Verdict = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunPhases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunPhases_RunId",
                table: "RunPhases",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunPhases_RunId_PhaseId",
                table: "RunPhases",
                columns: new[] { "RunId", "PhaseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunPhases");

            migrationBuilder.DropColumn(
                name: "PhaseId",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "PhaseId",
                table: "RunDecisions");
        }
    }
}
