using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddObservedCallers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObservedCallers",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    NameClaim = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    NameValue = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RoleValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupsOmitted = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservedCallers", x => x.Subject);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObservedCallers");
        }
    }
}
