using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeriesRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesRunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesRuns_SeriesRunId",
                table: "SeriesRuns",
                column: "SeriesRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesRuns_SessionId",
                table: "SeriesRuns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesRuns_State",
                table: "SeriesRuns",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeriesRuns");
        }
    }
}
