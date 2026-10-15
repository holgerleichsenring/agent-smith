using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedTicketFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClosedForWork",
                table: "TicketBetweenAttemptsChecks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "HeadMovedAtTicks",
                table: "PrReviewRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadSha",
                table: "PrReviewRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReopenedAtTicks",
                table: "PrReviewRecords",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedForWork",
                table: "TicketBetweenAttemptsChecks");

            migrationBuilder.DropColumn(
                name: "HeadMovedAtTicks",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "HeadSha",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "ReopenedAtTicks",
                table: "PrReviewRecords");
        }
    }
}
