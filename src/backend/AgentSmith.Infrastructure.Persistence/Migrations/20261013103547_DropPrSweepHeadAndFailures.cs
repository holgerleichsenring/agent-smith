using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-10-13-0b82a: the sweep no longer reviews a pull request on open or push, so the head it
    /// reviewed and the count of its failed reviews have nothing left to record.
    /// </summary>
    public partial class DropPrSweepHeadAndFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedReviews",
                table: "PrSweepStates");

            migrationBuilder.DropColumn(
                name: "ReviewedHead",
                table: "PrSweepStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedReviews",
                table: "PrSweepStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedHead",
                table: "PrSweepStates",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }
    }
}
