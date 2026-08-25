using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunRecordIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EventSeq",
                table: "RunSteps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EventSeq",
                table: "RunLlmCalls",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EventSeq",
                table: "RunDecisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSteps_RunId_EventSeq",
                table: "RunSteps",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunLlmCalls_RunId_EventSeq",
                table: "RunLlmCalls",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId_Seq",
                table: "RunEvents",
                columns: new[] { "RunId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunDecisions_RunId_EventSeq",
                table: "RunDecisions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RunSteps_RunId_EventSeq",
                table: "RunSteps");

            migrationBuilder.DropIndex(
                name: "IX_RunLlmCalls_RunId_EventSeq",
                table: "RunLlmCalls");

            migrationBuilder.DropIndex(
                name: "IX_RunEvents_RunId_Seq",
                table: "RunEvents");

            migrationBuilder.DropIndex(
                name: "IX_RunDecisions_RunId_EventSeq",
                table: "RunDecisions");

            migrationBuilder.DropColumn(
                name: "EventSeq",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "EventSeq",
                table: "RunLlmCalls");

            migrationBuilder.DropColumn(
                name: "EventSeq",
                table: "RunDecisions");
        }
    }
}
