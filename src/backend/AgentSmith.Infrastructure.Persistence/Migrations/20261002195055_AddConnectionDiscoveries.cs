using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionDiscoveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionDiscoveries",
                columns: table => new
                {
                    ConnectionName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReposJson = table.Column<string>(type: "TEXT", nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionDiscoveries", x => x.ConnectionName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionDiscoveries");
        }
    }
}
