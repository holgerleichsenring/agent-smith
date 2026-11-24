using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPocRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PocRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PocId = table.Column<long>(type: "INTEGER", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadSession = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    UploadId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    ManifestJson = table.Column<string>(type: "TEXT", nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Entrypoint = table.Column<string>(type: "TEXT", nullable: true),
                    BaseImage = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PocRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PocRevisions_PocId_Number",
                table: "PocRevisions",
                columns: new[] { "PocId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PocRevisions_UploadId",
                table: "PocRevisions",
                column: "UploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PocRevisions");
        }
    }
}
