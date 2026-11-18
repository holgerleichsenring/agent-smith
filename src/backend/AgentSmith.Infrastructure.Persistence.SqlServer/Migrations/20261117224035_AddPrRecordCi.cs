using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrRecordCi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CiAtTicks",
                table: "PrReviewRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CiFailuresJson",
                table: "PrReviewRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CiHeadSha",
                table: "PrReviewRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CiState",
                table: "PrReviewRecords",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CiAtTicks",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "CiFailuresJson",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "CiHeadSha",
                table: "PrReviewRecords");

            migrationBuilder.DropColumn(
                name: "CiState",
                table: "PrReviewRecords");
        }
    }
}
