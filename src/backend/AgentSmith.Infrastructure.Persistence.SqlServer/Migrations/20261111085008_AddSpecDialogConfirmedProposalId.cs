using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                type: "nvarchar(64)",
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
