using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tracker = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    By = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedPullRequestsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    TrackerClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedTickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedTickets_Project",
                table: "ClosedTickets",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_ClosedTickets_Tracker_TicketKey",
                table: "ClosedTickets",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosedTickets");
        }
    }
}
