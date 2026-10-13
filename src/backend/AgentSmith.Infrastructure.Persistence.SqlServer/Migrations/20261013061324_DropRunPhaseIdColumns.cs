using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// 2026-10-12-967ah: the old spec-id column goes. A row only an older replica wrote has its
    /// SpecId filled from PhaseId first, then both PhaseId columns are dropped; going down restores
    /// them and fills them from SpecId.
    /// </summary>
    public partial class DropRunPhaseIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Copy(migrationBuilder, "RunSteps", from: "PhaseId", to: "SpecId");
            Copy(migrationBuilder, "RunDecisions", from: "PhaseId", to: "SpecId");

            migrationBuilder.DropColumn(
                name: "PhaseId",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "PhaseId",
                table: "RunDecisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhaseId",
                table: "RunSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhaseId",
                table: "RunDecisions",
                type: "nvarchar(max)",
                nullable: true);

            Copy(migrationBuilder, "RunSteps", from: "SpecId", to: "PhaseId");
            Copy(migrationBuilder, "RunDecisions", from: "SpecId", to: "PhaseId");
        }

        private static void Copy(MigrationBuilder migrationBuilder, string table, string from, string to) =>
            migrationBuilder.Sql(
                $"UPDATE [{table}] SET [{to}] = [{from}] WHERE [{to}] IS NULL AND [{from}] IS NOT NULL;");
    }
}
