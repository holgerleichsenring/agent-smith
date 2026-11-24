using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPocRollout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RolloutAtTicks",
                table: "PocRevisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutCommitSha",
                table: "PocRevisions",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutState",
                table: "PocRevisions",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RolloutAtTicks",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "RolloutCommitSha",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "RolloutState",
                table: "PocRevisions");
        }
    }
}
