using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    By = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    ClosedPullRequestsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    TrackerClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
