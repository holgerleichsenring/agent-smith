using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-10-12-967ab: the spec id gets its own column beside PhaseId (EF maps one column per
    /// property, so a reader cannot accept two names). Both are written until 2026-10-12-967ah
    /// drops PhaseId; the rows written before are backfilled here, quoted for the provider.
    /// </summary>
    public partial class AddRunSpecIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunSteps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunDecisions",
                type: "TEXT",
                nullable: true);

            Backfill(migrationBuilder, "RunSteps");
            Backfill(migrationBuilder, "RunDecisions");
        }

        // MySQL quotes identifiers with backticks; SQLite and PostgreSQL with double quotes, which
        // PostgreSQL needs because EF created these tables under mixed-case names.
        private static void Backfill(MigrationBuilder migrationBuilder, string table)
        {
            var (open, close) = migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql"
                ? ("`", "`") : ("\"", "\"");
            migrationBuilder.Sql(
                $"UPDATE {open}{table}{close} SET {open}SpecId{close} = {open}PhaseId{close} "
                + $"WHERE {open}SpecId{close} IS NULL AND {open}PhaseId{close} IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RunDecisions");
        }
    }
}
