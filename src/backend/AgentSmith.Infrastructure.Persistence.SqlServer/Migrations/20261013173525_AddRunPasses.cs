using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MasterPass",
                table: "RunLlmCalls",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunLlmCalls",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunLedgerTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MasterPass = table.Column<int>(type: "int", nullable: true),
                    TransitionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLedgerTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunPasses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    PassInStep = table.Column<int>(type: "int", nullable: false),
                    OpenedBecause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndedBecause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReengageReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerdictEmitted = table.Column<bool>(type: "bit", nullable: false),
                    CriteriaMet = table.Column<int>(type: "int", nullable: true),
                    CriteriaTotal = table.Column<int>(type: "int", nullable: true),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GateLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionablePending = table.Column<int>(type: "int", nullable: false),
                    UnbackedDone = table.Column<int>(type: "int", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunPasses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunLedgerTransitions_RunId",
                table: "RunLedgerTransitions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunLedgerTransitions_RunId_EventSeq",
                table: "RunLedgerTransitions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true,
                filter: "[EventSeq] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId",
                table: "RunPasses",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId_EventSeq",
                table: "RunPasses",
                columns: new[] { "RunId", "EventSeq" },
                unique: true,
                filter: "[EventSeq] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunLedgerTransitions");

            migrationBuilder.DropTable(
                name: "RunPasses");

            migrationBuilder.DropColumn(
                name: "MasterPass",
                table: "RunLlmCalls");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RunLlmCalls");
        }
    }
}
