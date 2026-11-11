using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDialogConfirmedProposalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmedProposalId",
                table: "SpecDialogSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedProposalId",
                table: "SpecDialogSessions");
        }
    }
}
