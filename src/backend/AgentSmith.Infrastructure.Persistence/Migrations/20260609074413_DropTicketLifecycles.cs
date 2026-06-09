using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTicketLifecycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketLifecycles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketLifecycles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketLifecycles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketLifecycles_Project_Platform_TicketId",
                table: "TicketLifecycles",
                columns: new[] { "Project", "Platform", "TicketId" },
                unique: true);
        }
    }
}
