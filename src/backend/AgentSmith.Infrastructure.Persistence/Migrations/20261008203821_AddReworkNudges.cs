using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReworkNudges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActsReadAt",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReworkLedgers",
                columns: table => new
                {
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    NotServedThroughTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    SpokenKeys = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkLedgers", x => new { x.Project, x.TicketId });
                });

            migrationBuilder.CreateTable(
                name: "ReworkNudges",
                columns: table => new
                {
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    PrUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    DueTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Generation = table.Column<long>(type: "INTEGER", nullable: false),
                    ClaimToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Tries = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkNudges", x => new { x.Project, x.TicketId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReworkNudges_DueTicks",
                table: "ReworkNudges",
                column: "DueTicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReworkLedgers");

            migrationBuilder.DropTable(
                name: "ReworkNudges");

            migrationBuilder.DropColumn(
                name: "ActsReadAt",
                table: "Runs");
        }
    }
}
