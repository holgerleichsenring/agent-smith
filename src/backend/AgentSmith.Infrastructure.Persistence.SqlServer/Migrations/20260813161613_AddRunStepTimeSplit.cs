using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunStepTimeSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LlmMs",
                table: "RunSteps",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SandboxMs",
                table: "RunSteps",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ThrottleWaitMs",
                table: "RunSteps",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmMs",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "SandboxMs",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "ThrottleWaitMs",
                table: "RunSteps");
        }
    }
}
