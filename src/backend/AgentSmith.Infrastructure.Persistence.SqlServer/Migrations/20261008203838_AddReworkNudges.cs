using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReworkLedgers",
                columns: table => new
                {
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    NotServedThroughTicks = table.Column<long>(type: "bigint", nullable: true),
                    SpokenKeys = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkLedgers", x => new { x.Project, x.TicketId });
                });

            migrationBuilder.CreateTable(
                name: "ReworkNudges",
                columns: table => new
                {
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    PrUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    DueTicks = table.Column<long>(type: "bigint", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ClaimToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Tries = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
