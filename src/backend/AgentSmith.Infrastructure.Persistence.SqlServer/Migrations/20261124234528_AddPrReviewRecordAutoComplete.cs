using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrReviewRecordAutoComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoCompleteArmed",
                table: "PrReviewRecords",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AutoCompleteLostAtTicks",
                table: "PrReviewRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoCompleteRearmedHead",
                table: "PrReviewRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoCompleteArmed",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "AutoCompleteLostAtTicks",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "AutoCompleteRearmedHead",
                table: "PrReviewRecords");
        }
    }
}
