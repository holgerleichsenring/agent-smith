using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameRunPhasesToRunSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-10-06-03c7g: a run's progress is counted in specs. A RENAME, not the
            // drop-and-create the scaffolder proposes, so every stored run keeps its rows.
            migrationBuilder.RenameTable(
                name: "RunPhases",
                newName: "RunSpecs");

            migrationBuilder.RenameColumn(
                name: "PhaseId",
                table: "RunSpecs",
                newName: "SpecId");

            migrationBuilder.RenameIndex(
                name: "IX_RunPhases_RunId",
                table: "RunSpecs",
                newName: "IX_RunSpecs_RunId");

            migrationBuilder.RenameIndex(
                name: "IX_RunPhases_RunId_PhaseId",
                table: "RunSpecs",
                newName: "IX_RunSpecs_RunId_SpecId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: the table first, so each index is renamed on the table the
            // previous model knows (SQLite rebuilds an index from that model).
            migrationBuilder.RenameTable(
                name: "RunSpecs",
                newName: "RunPhases");

            migrationBuilder.RenameColumn(
                name: "SpecId",
                table: "RunPhases",
                newName: "PhaseId");

            migrationBuilder.RenameIndex(
                name: "IX_RunSpecs_RunId",
                table: "RunPhases",
                newName: "IX_RunPhases_RunId");

            migrationBuilder.RenameIndex(
                name: "IX_RunSpecs_RunId_SpecId",
                table: "RunPhases",
                newName: "IX_RunPhases_RunId_PhaseId");
        }
    }
}
