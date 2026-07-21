using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRunBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BudgetCapTokens",
                table: "Runs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetCapUsd",
                table: "Runs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetTier",
                table: "Runs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetCapTokens",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "BudgetCapUsd",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "BudgetTier",
                table: "Runs");
        }
    }
}
