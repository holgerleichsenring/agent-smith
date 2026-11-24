using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPocScanVerdicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "PocRevisions",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "PocRevisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverruledIdsJson",
                table: "PocRevisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerdictJson",
                table: "PocRevisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FindingIdsJson",
                table: "PocAuditEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "OverruledIdsJson",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "VerdictJson",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "FindingIdsJson",
                table: "PocAuditEntries");
        }
    }
}
