using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLandedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LandedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    By = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PrUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedTickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandedTickets_Project",
                table: "LandedTickets",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_LandedTickets_Tracker_TicketKey",
                table: "LandedTickets",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandedTickets");
        }
    }
}
