using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddUnmovedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnmovedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ConfiguredStatus = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    TrackerConfigVersion = table.Column<int>(type: "int", nullable: false),
                    ProjectConfigVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnmovedTickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnmovedTickets_Project_TicketId",
                table: "UnmovedTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnmovedTickets");
        }
    }
}
