using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPocs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PocAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PocId = table.Column<long>(type: "INTEGER", nullable: false),
                    AtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Act = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PocAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pocs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 63, nullable: false),
                    LiveName = table.Column<string>(type: "TEXT", maxLength: 63, nullable: true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 63, nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Replicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageDigest = table.Column<string>(type: "TEXT", maxLength: 71, nullable: true),
                    ExpiresAtTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    LastRunId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pocs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PocAuditEntries_PocId_AtTicks",
                table: "PocAuditEntries",
                columns: new[] { "PocId", "AtTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_PocAuditEntries_RunId_Act",
                table: "PocAuditEntries",
                columns: new[] { "RunId", "Act" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pocs_Project",
                table: "Pocs",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_Pocs_Target_LiveName",
                table: "Pocs",
                columns: new[] { "Target", "LiveName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PocAuditEntries");

            migrationBuilder.DropTable(
                name: "Pocs");
        }
    }
}
