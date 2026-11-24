using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddPrReviewRecordConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConflictsAtTicks",
                table: "PrReviewRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasConflicts",
                table: "PrReviewRecords",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConflictsAtTicks",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "HasConflicts",
                table: "PrReviewRecords");
        }
    }
}
