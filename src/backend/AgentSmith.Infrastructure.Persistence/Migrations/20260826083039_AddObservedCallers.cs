using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
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
                    Subject = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    NameClaim = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    NameValue = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RoleValues = table.Column<string>(type: "TEXT", nullable: false),
                    GroupValues = table.Column<string>(type: "TEXT", nullable: false),
                    GroupsOmitted = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
