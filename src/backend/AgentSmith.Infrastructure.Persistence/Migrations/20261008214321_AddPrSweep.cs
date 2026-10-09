using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
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
                    Repository = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Initialised = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSweptTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrSweepRepositories", x => x.Repository);
                });

            migrationBuilder.CreateTable(
                name: "PrSweepStates",
                columns: table => new
                {
                    Repository = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    ReviewedHead = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LabelPresent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommentsSeenTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CommentsSeenId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
