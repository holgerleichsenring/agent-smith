using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Project",
                table: "ApprovedSeries",
                type: "TEXT",
                maxLength: 191,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TicketRequirements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RequiredTicketKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RequiredTicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    StartsWhenMet = table.Column<bool>(type: "INTEGER", nullable: false),
                    FiledInConversation = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRequirements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Project",
                table: "ApprovedSeries",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Project",
                table: "TicketRequirements",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Tracker_RequiredTicketKey",
                table: "TicketRequirements",
                columns: new[] { "Tracker", "RequiredTicketKey" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Tracker_TicketKey_RequiredTicketKey",
                table: "TicketRequirements",
                columns: new[] { "Tracker", "TicketKey", "RequiredTicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketRequirements");

            migrationBuilder.DropIndex(
                name: "IX_ApprovedSeries_Project",
                table: "ApprovedSeries");

            migrationBuilder.DropColumn(
                name: "Project",
                table: "ApprovedSeries");
        }
    }
}
