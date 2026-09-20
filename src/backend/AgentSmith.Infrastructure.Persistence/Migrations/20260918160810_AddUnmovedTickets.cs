using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ConfiguredStatus = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackerConfigVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectConfigVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
