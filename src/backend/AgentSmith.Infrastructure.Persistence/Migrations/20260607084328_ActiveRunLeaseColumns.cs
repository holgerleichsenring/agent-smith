using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActiveRunLeaseColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns");

            migrationBuilder.AlterColumn<string>(
                name: "RunId",
                table: "ActiveRuns",
                type: "TEXT",
                maxLength: 191,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 191);

            migrationBuilder.AddColumn<string>(
                name: "JobId",
                table: "ActiveRuns",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "ActiveRuns");

            migrationBuilder.AlterColumn<string>(
                name: "RunId",
                table: "ActiveRuns",
                type: "TEXT",
                maxLength: 191,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 191,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveRuns_Runs_RunId",
                table: "ActiveRuns",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
