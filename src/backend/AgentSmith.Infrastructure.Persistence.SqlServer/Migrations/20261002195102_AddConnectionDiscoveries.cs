using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    ConnectionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReposJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
