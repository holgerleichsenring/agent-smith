using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketWorkSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketWorkSpecs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SpecKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CarryingRepo = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RevisionSha = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHandbackCase = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatedHandbackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HandbackSourceSha = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketWorkSpecs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketWorkSpecs_Project_SpecKey",
                table: "TicketWorkSpecs",
                columns: new[] { "Project", "SpecKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketWorkSpecs");
        }
    }
}
