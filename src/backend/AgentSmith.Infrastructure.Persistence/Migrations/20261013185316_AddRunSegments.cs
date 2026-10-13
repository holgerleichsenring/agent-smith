using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunSegments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndStatus = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSegments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunSegments_RunId",
                table: "RunSegments",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSegments_RunId_EventSeq",
                table: "RunSegments",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunSegments");
        }
    }
}
