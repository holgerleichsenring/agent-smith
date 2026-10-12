using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// 2026-10-12-967ab: the spec id gets its own column beside PhaseId (EF maps one column per
    /// property, so a reader cannot accept two names). Both are written until 2026-10-12-967ah
    /// drops PhaseId; the rows written before are backfilled here.
    /// </summary>
    public partial class AddRunSpecIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "RunDecisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [RunSteps] SET [SpecId] = [PhaseId] WHERE [SpecId] IS NULL AND [PhaseId] IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE [RunDecisions] SET [SpecId] = [PhaseId] WHERE [SpecId] IS NULL AND [PhaseId] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RunSteps");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RunDecisions");
        }
    }
}
