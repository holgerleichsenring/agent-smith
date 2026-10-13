using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// 2026-10-13-9a59i: a sandbox row records how it ended instead of a status. Status held one recorded
    /// fact — 'vanished' — which is copied to EndReason before Status is dropped; 'failed' and 'ok' were
    /// never facts and every other old row reads null. The create's trail position becomes the row's
    /// identity: unique (RunId, EventSeq), filtered to the rows that have one.
    /// </summary>
    public partial class RecordSandboxEndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "RunSandboxes",
                type: "nvarchar(max)",
                nullable: true);

            Copy(migrationBuilder, from: "Status", to: "EndReason");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RunSandboxes");

            migrationBuilder.AddColumn<long>(
                name: "EventSeq",
                table: "RunSandboxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitCode",
                table: "RunSandboxes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OomKilled",
                table: "RunSandboxes",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSandboxes_RunId_EventSeq",
                table: "RunSandboxes",
                columns: new[] { "RunId", "EventSeq" },
                unique: true,
                filter: "[EventSeq] IS NOT NULL");
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
                type: "nvarchar(max)",
                nullable: true);

            Copy(migrationBuilder, from: "EndReason", to: "Status");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "RunSandboxes");
        }

        private static void Copy(MigrationBuilder migrationBuilder, string from, string to) =>
            migrationBuilder.Sql(
                $"UPDATE [RunSandboxes] SET [{to}] = 'vanished' WHERE [{from}] = 'vanished';");
    }
}
