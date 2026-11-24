using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPocVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StoredSecrets",
                table: "StoredSecrets");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "StoredSecrets",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "installation");

            migrationBuilder.AddColumn<string>(
                name: "PlainVariablesJson",
                table: "Pocs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SealFingerprint",
                table: "Pocs",
                type: "nvarchar(76)",
                maxLength: 76,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StoredSecrets",
                table: "StoredSecrets",
                columns: new[] { "Scope", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StoredSecrets",
                table: "StoredSecrets");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "StoredSecrets");

            migrationBuilder.DropColumn(
                name: "PlainVariablesJson",
                table: "Pocs");

            migrationBuilder.DropColumn(
                name: "SealFingerprint",
                table: "Pocs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StoredSecrets",
                table: "StoredSecrets",
                column: "Name");
        }
    }
}
