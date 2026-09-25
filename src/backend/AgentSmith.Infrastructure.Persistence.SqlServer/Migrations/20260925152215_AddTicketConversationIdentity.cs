using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketConversationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TicketKey",
                table: "SpecDialogSessions",
                type: "nvarchar(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracker",
                table: "SpecDialogSessions",
                type: "nvarchar(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_Tracker_TicketKey",
                table: "SpecDialogSessions",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true,
                filter: "[Tracker] IS NOT NULL AND [TicketKey] IS NOT NULL");
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
