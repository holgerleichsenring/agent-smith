using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunPlannedSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlannedFirstStepIndex",
                table: "Runs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedStepsJson",
                table: "Runs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedFirstStepIndex",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "PlannedStepsJson",
                table: "Runs");
        }
    }
}
