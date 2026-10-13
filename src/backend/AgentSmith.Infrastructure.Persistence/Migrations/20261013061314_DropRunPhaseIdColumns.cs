using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-10-12-967ah: the old spec-id column goes. A row only an older replica wrote has its
    /// SpecId filled from PhaseId first, then both PhaseId columns are dropped; going down restores
    /// them and fills them from SpecId. Quoted for the provider, as AddRunSpecIdColumns is.
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhaseId",
                table: "RunDecisions",
                type: "TEXT",
                nullable: true);

            Copy(migrationBuilder, "RunSteps", from: "SpecId", to: "PhaseId");
            Copy(migrationBuilder, "RunDecisions", from: "SpecId", to: "PhaseId");
        }

        // MySQL quotes identifiers with backticks; SQLite and PostgreSQL with double quotes, which
        // PostgreSQL needs because EF created these tables under mixed-case names.
        private static void Copy(MigrationBuilder migrationBuilder, string table, string from, string to)
        {
            var (open, close) = migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql"
                ? ("`", "`") : ("\"", "\"");
            migrationBuilder.Sql(
                $"UPDATE {open}{table}{close} SET {open}{to}{close} = {open}{from}{close} "
                + $"WHERE {open}{to}{close} IS NULL AND {open}{from}{close} IS NOT NULL;");
        }
    }
}
