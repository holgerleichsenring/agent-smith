using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunStepAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StepIndex",
                table: "RunSandboxes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepIndex",
                table: "RunLlmCalls",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepIndex",
                table: "RunEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepIndex",
                table: "RunDecisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId_StepIndex_Seq",
                table: "RunEvents",
                columns: new[] { "RunId", "StepIndex", "Seq" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RunEvents_RunId_StepIndex_Seq",
                table: "RunEvents");

            migrationBuilder.DropColumn(
                name: "StepIndex",
                table: "RunSandboxes");

            migrationBuilder.DropColumn(
                name: "StepIndex",
                table: "RunLlmCalls");

            migrationBuilder.DropColumn(
                name: "StepIndex",
                table: "RunEvents");

            migrationBuilder.DropColumn(
                name: "StepIndex",
                table: "RunDecisions");
        }
    }
}
