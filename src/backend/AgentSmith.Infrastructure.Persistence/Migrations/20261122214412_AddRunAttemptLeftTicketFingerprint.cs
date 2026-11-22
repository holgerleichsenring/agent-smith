using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>2026-11-22-e812: the text a run wrote into the tracker, beside the one it fetched.</summary>
    public partial class AddRunAttemptLeftTicketFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeftTicketFingerprint",
                table: "RunAttempts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftTicketFingerprint",
                table: "RunAttempts");
        }
    }
}
