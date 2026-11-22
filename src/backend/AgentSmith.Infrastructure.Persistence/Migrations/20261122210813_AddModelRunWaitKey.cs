using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelRunWaitKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "ModelRunWaits",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelRunWaits_Key",
                table: "ModelRunWaits",
                column: "Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelRunWaits_Key",
                table: "ModelRunWaits");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "ModelRunWaits");
        }
    }
}
