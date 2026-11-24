using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                type: "nvarchar(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "PocRevisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverruledIdsJson",
                table: "PocRevisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerdictJson",
                table: "PocRevisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FindingIdsJson",
                table: "PocAuditEntries",
                type: "nvarchar(max)",
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
