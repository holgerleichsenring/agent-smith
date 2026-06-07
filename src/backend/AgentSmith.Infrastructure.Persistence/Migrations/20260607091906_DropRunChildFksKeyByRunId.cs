using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRunChildFksKeyByRunId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_RunArtifacts_Runs_RunId",
                table: "RunArtifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_RunDecisions_Runs_RunId",
                table: "RunDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_RunEvents_Runs_RunId",
                table: "RunEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RunLlmCalls_Runs_RunId",
                table: "RunLlmCalls");

            migrationBuilder.DropForeignKey(
                name: "FK_RunRepos_Runs_RunId",
                table: "RunRepos");

            migrationBuilder.DropForeignKey(
                name: "FK_RunSandboxes_Runs_RunId",
                table: "RunSandboxes");

            migrationBuilder.DropForeignKey(
                name: "FK_RunSteps_Runs_RunId",
                table: "RunSteps");

            migrationBuilder.DropIndex(
                name: "IX_ActiveRuns_RunId",
                table: "ActiveRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ActiveRuns_RunId",
                table: "ActiveRuns",
                column: "RunId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RunArtifacts_Runs_RunId",
                table: "RunArtifacts",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunDecisions_Runs_RunId",
                table: "RunDecisions",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunEvents_Runs_RunId",
                table: "RunEvents",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunLlmCalls_Runs_RunId",
                table: "RunLlmCalls",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunRepos_Runs_RunId",
                table: "RunRepos",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunSandboxes_Runs_RunId",
                table: "RunSandboxes",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RunSteps_Runs_RunId",
                table: "RunSteps",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
