using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-10-13-9a59i: a sandbox row records how it ended instead of a status. Status held one recorded
    /// fact — 'vanished' — which is copied to EndReason before Status is dropped; 'failed' and 'ok' were
    /// never facts and every other old row reads null. The create's trail position becomes the row's
    /// identity: unique (RunId, EventSeq).
    /// </summary>
    public partial class RecordSandboxEndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "RunSandboxes",
                type: "TEXT",
                nullable: true);

            Copy(migrationBuilder, from: "Status", to: "EndReason");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RunSandboxes");

            migrationBuilder.AddColumn<long>(
                name: "EventSeq",
                table: "RunSandboxes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitCode",
                table: "RunSandboxes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OomKilled",
                table: "RunSandboxes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSandboxes_RunId_EventSeq",
                table: "RunSandboxes",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RunSandboxes_RunId_EventSeq",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "EventSeq",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "ExitCode",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "OomKilled",
                table: "RunSandboxes");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RunSandboxes",
                type: "TEXT",
                nullable: true);

            Copy(migrationBuilder, from: "EndReason", to: "Status");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "RunSandboxes");
        }

        // MySQL quotes identifiers with backticks; SQLite and PostgreSQL with double quotes, which
        // PostgreSQL needs because EF created these tables under mixed-case names.
        private static void Copy(MigrationBuilder migrationBuilder, string from, string to)
        {
            var (open, close) = migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql"
                ? ("`", "`") : ("\"", "\"");
            migrationBuilder.Sql(
                $"UPDATE {open}RunSandboxes{close} SET {open}{to}{close} = 'vanished' "
                + $"WHERE {open}{from}{close} = 'vanished';");
        }
    }
}
