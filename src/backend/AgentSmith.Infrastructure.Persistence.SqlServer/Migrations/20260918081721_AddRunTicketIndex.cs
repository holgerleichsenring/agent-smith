using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunTicketIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Runs_Project_TicketId",
                table: "Runs",
                columns: new[] { "Project", "TicketId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Runs_Project_TicketId",
                table: "Runs");
        }
    }
}
