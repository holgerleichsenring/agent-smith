using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesRunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
