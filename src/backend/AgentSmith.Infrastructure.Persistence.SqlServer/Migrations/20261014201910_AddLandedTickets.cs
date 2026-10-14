using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tracker = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LandedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    By = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
