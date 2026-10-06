using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTicketSpecSetsWithTicketSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSpecSets");

            migrationBuilder.CreateTable(
                name: "TicketSeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    SeriesId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CarryingRepo = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RevisionSha = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    LastHandbackCase = table.Column<int>(type: "int", nullable: false),
                    RepeatedHandbackCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSeries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSeries_Project_TicketKey",
                table: "TicketSeries",
                columns: new[] { "Project", "TicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSeries");

            migrationBuilder.CreateTable(
                name: "TicketSpecSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarryingRepo = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HandbackSourceSha = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: true),
                    LastHandbackCase = table.Column<int>(type: "int", nullable: false),
                    Project = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    RepeatedHandbackCount = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    RevisionSha = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    SpecKey = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSpecSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSpecSets_Project_SpecKey",
                table: "TicketSpecSets",
                columns: new[] { "Project", "SpecKey" },
                unique: true);
        }
    }
}
