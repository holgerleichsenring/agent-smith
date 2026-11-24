using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
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
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutCommitSha",
                table: "PocRevisions",
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloutState",
                table: "PocRevisions",
                type: "character varying(16)",
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
