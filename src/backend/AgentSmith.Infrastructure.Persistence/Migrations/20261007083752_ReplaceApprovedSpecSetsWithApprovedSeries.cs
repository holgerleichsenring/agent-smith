using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceApprovedSpecSetsWithApprovedSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedSpecSets");

            migrationBuilder.CreateTable(
                name: "ApprovedSeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SeriesId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Repositories = table.Column<string>(type: "TEXT", nullable: false),
                    CarryingRepo = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ApprovedInConversation = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SatisfiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSeries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Tracker_SatisfiedAt",
                table: "ApprovedSeries",
                columns: new[] { "Tracker", "SatisfiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Tracker_TicketKey",
                table: "ApprovedSeries",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedSeries");

            migrationBuilder.CreateTable(
                name: "ApprovedSpecSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ApprovedInConversation = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false),
                    SatisfiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SpecKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSpecSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSpecSets_Tracker_SatisfiedAt",
                table: "ApprovedSpecSets",
                columns: new[] { "Tracker", "SatisfiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSpecSets_Tracker_SpecKey",
                table: "ApprovedSpecSets",
                columns: new[] { "Tracker", "SpecKey" },
                unique: true);
        }
    }
}
