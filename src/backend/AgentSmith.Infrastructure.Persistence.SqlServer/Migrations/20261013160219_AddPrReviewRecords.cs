using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HostId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Standing = table.Column<bool>(type: "bit", nullable: false),
                    AuthorLogin = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Trusted = table.Column<bool>(type: "bit", nullable: false),
                    IsOurs = table.Column<bool>(type: "bit", nullable: false),
                    IsBot = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    File = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Line = table.Column<int>(type: "int", nullable: true),
                    Resolved = table.Column<bool>(type: "bit", nullable: true),
                    ThreadHostId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Truncated = table.Column<bool>(type: "bit", nullable: false),
                    AtTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrReviewEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrReviewRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Repo = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RepoKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StateAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    CheckedAtTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
