using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunSeriesCuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "RunSpecs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceJson",
                table: "RunSpecs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunSeriesCuts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    SetKey = table.Column<string>(type: "TEXT", nullable: false),
                    HandbackCase = table.Column<string>(type: "TEXT", nullable: true),
                    SpecsJson = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSeriesCuts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunSeriesCuts_RunId",
                table: "RunSeriesCuts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSeriesCuts_RunId_EventSeq",
                table: "RunSeriesCuts",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunSeriesCuts");

            migrationBuilder.DropColumn(
                name: "AcceptanceJson",
                table: "RunSpecs");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "RunSpecs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
