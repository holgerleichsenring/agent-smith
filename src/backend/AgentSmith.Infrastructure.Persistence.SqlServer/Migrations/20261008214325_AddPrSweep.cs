using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrSweep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrSweepRepositories",
                columns: table => new
                {
                    Repository = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Initialised = table.Column<bool>(type: "bit", nullable: false),
                    LastSweptTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrSweepRepositories", x => x.Repository);
                });

            migrationBuilder.CreateTable(
                name: "PrSweepStates",
                columns: table => new
                {
                    Repository = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    ReviewedHead = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LabelPresent = table.Column<bool>(type: "bit", nullable: false),
                    CommentsSeenTicks = table.Column<long>(type: "bigint", nullable: false),
                    CommentsSeenId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrSweepStates", x => new { x.Repository, x.Number });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrSweepRepositories");

            migrationBuilder.DropTable(
                name: "PrSweepStates");
        }
    }
}
