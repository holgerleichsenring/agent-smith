using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
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
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunLlmCalls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunLedgerTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    SpecId = table.Column<string>(type: "TEXT", nullable: true),
                    MasterPass = table.Column<int>(type: "INTEGER", nullable: true),
                    TransitionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLedgerTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunPasses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    SpecId = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    PassInStep = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedBecause = table.Column<string>(type: "TEXT", nullable: false),
                    EndedBecause = table.Column<string>(type: "TEXT", nullable: false),
                    ReengageReason = table.Column<string>(type: "TEXT", nullable: true),
                    VerdictEmitted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriteriaMet = table.Column<int>(type: "INTEGER", nullable: true),
                    CriteriaTotal = table.Column<int>(type: "INTEGER", nullable: true),
                    CriteriaJson = table.Column<string>(type: "TEXT", nullable: true),
                    GateLine = table.Column<string>(type: "TEXT", nullable: false),
                    ActionablePending = table.Column<int>(type: "INTEGER", nullable: false),
                    UnbackedDone = table.Column<int>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId",
                table: "RunPasses",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId_EventSeq",
                table: "RunPasses",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
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
