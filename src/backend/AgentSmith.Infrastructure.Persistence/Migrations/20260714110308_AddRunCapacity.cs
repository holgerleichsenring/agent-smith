using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunCapacities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    FootprintJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalCpuNanos = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMemBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Reserved = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCapacities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_Reserved",
                table: "RunCapacities",
                column: "Reserved");

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_RunId",
                table: "RunCapacities",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunCapacities");
        }
    }
}
