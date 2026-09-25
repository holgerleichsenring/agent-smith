using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketConversationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-25-8e51b: NO type argument, for the reason AddSpecDialogSubject gives —
            // this set is generated from SQLite and three providers run it verbatim, and "TEXT"
            // on MySQL is a blob type that ignores the length beside it. Untyped, each provider's
            // own convention maps a bounded string to its bounded-string type.
            migrationBuilder.AddColumn<string>(
                name: "TicketKey",
                table: "SpecDialogSessions",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracker",
                table: "SpecDialogSessions",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_Tracker_TicketKey",
                table: "SpecDialogSessions",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpecDialogSessions_Tracker_TicketKey",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "TicketKey",
                table: "SpecDialogSessions");

            migrationBuilder.DropColumn(
                name: "Tracker",
                table: "SpecDialogSessions");
        }
    }
}
