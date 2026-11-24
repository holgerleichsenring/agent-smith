using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddPocBuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockedStage",
                table: "PocRevisions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Digest",
                table: "PocRevisions",
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageVerdictJson",
                table: "PocRevisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipelineRunId",
                table: "PocRevisions",
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipelineUrl",
                table: "PocRevisions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockedStage",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "Digest",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "ImageVerdictJson",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "PipelineRunId",
                table: "PocRevisions");

            migrationBuilder.DropColumn(
                name: "PipelineUrl",
                table: "PocRevisions");
        }
    }
}
