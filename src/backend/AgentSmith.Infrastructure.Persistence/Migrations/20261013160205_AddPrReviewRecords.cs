using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrReviewRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrReviewEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    HostId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Standing = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthorLogin = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Trusted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOurs = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBot = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    File = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Line = table.Column<int>(type: "INTEGER", nullable: true),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: true),
                    ThreadHostId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Truncated = table.Column<bool>(type: "INTEGER", nullable: false),
                    AtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrReviewEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrReviewRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Repo = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RepoKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StateAtTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    CheckedAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrReviewRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewEntries_PrKey_HostId",
                table: "PrReviewEntries",
                columns: new[] { "PrKey", "HostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_PrKey",
                table: "PrReviewRecords",
                column: "PrKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_Project_TicketId",
                table: "PrReviewRecords",
                columns: new[] { "Project", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_RepoKey_Number",
                table: "PrReviewRecords",
                columns: new[] { "RepoKey", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_RunId",
                table: "PrReviewRecords",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrReviewEntries");

            migrationBuilder.DropTable(
                name: "PrReviewRecords");
        }
    }
}
