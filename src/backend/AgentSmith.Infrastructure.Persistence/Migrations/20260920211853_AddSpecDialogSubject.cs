using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-20-4b0af: NO type argument, deliberately — the same reason
            // AddSpecDialogAttachments gives. This set is generated from SQLite and three
            // providers run it verbatim, so a literal here is provider-blind: "TEXT" on MySQL
            // is a sixty-four-kilobyte blob type that ignores the length beside it, and the
            // length is the whole declaration of this column. Untyped, each provider's own
            // convention maps a bounded string to that provider's bounded-string type.
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "SpecDialogSessions",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                table: "SpecDialogSessions");
        }
    }
}
